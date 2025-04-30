﻿/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using Sentry;

namespace eFormAPI.Web.Services;

using Cache.AuthCache;
using Infrastructure.Models.Auth;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Abstractions;
using eFormAPI.Web.Abstractions.Security;
using PureOtp;
using Hosting.Helpers.DbOptions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microting.EformAngularFrontendBase.Infrastructure.Const;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Database.Entities;
using Microting.eFormApi.BasePn.Infrastructure.Helpers;
using Microting.eFormApi.BasePn.Infrastructure.Models.Application;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.eFormApi.BasePn.Infrastructure.Models.Auth;

public class AuthService(
    IOptions<EformTokenOptions> tokenOptions,
    ILogger<AuthService> logger,
    IDbOptions<ApplicationSettings> appSettings,
    RoleManager<EformRole> roleManager,
    SignInManager<EformUser> signInManager,
    UserManager<EformUser> userManager,
    IUserService userService,
    ILocalizationService localizationService,
    IClaimsService claimsService,
    IAuthCacheService authCacheService)
    : IAuthService
{
    public async Task<OperationDataResult<EformAuthorizeResult>> AuthenticateUser(LoginModel model)
    {
        Log.LogEvent("AuthService.AuthenticateUser: called");
        if (string.IsNullOrEmpty(model.Username) || string.IsNullOrEmpty(model.Password))
            return new OperationDataResult<EformAuthorizeResult>(false, "Empty username or password");

        var user = await userService.GetByUsernameAsync(model.Username);
        if (user == null)
            return new OperationDataResult<EformAuthorizeResult>(false,
                $"User with username {model.Username} not found");

        var signInResult =
            await signInManager.CheckPasswordSignInAsync(user, model.Password, true);

        if (!signInResult.Succeeded && !signInResult.RequiresTwoFactor)
        {
            if (signInResult.IsLockedOut)
            {
                return new OperationDataResult<EformAuthorizeResult>(false,
                    "Locked Out. Please, try again after 10 min");
            }

            // Credentials are invalid, or account doesn't exist
            return new OperationDataResult<EformAuthorizeResult>(false, "Incorrect password.");
        }

        // Confirmed email check
        if (!user.EmailConfirmed)
        {
            return new OperationDataResult<EformAuthorizeResult>(false, $"Email {user.Email} not confirmed");
        }

        // TwoFactor check
        var psk = user.GoogleAuthenticatorSecretKey;
        var code = model.Code;
        var isTwoFactorAuthForced = appSettings.Value.IsTwoFactorForced;
        if (user.TwoFactorEnabled || isTwoFactorAuthForced)
        {
            // check input params
            if (string.IsNullOrEmpty(psk) || string.IsNullOrEmpty(code))
            {
                return new OperationDataResult<EformAuthorizeResult>(false, "PSK or code is empty");
            }

            if (psk != user.GoogleAuthenticatorSecretKey)
            {
                return new OperationDataResult<EformAuthorizeResult>(false, "PSK is invalid");
            }

            // check code
            var otp = new Totp(Base32.FromBase32String(user.GoogleAuthenticatorSecretKey));
            var isCodeValid = otp.VerifyTotp(code, out _, new VerificationWindow(300, 300));
            if (!isCodeValid)
            {
                return new OperationDataResult<EformAuthorizeResult>(false, "Invalid code");
            }

            // update user entity
            if (!user.IsGoogleAuthenticatorEnabled)
            {
                user.IsGoogleAuthenticatorEnabled = true;
                var updateResult = userManager.UpdateAsync(user).Result;
                if (!updateResult.Succeeded)
                {
                    return new OperationDataResult<EformAuthorizeResult>(false, "PSK or code is empty");
                }
            }
        }

        var token = await GenerateToken(user);
        var roleList = userManager.GetRolesAsync(user).Result;
        if (!roleList.Any())
        {
            return new OperationDataResult<EformAuthorizeResult>(false,
                $"Role for user {model.Username} not found");
        }
        if (user.TimeZone == null)
        {
            user.TimeZone = "Europe/Copenhagen";
            await userManager.UpdateAsync(user);
        }
        if (user.Formats == null)
        {
            user.Formats = "de-DE";
            await userManager.UpdateAsync(user);
        }

        var firstUserIdInDb = await userService.GetFirstUserIdInDb();

        return new OperationDataResult<EformAuthorizeResult>(true, new EformAuthorizeResult
        {
            Id = user.Id,
            AccessToken = token.token,
            UserName = user.UserName,
            Role = roleList.FirstOrDefault(),
            ExpiresIn = token.expireIn,
            FirstName = user.FirstName,
            LastName = user.LastName,
            IsFirstUser = user.Id == firstUserIdInDb
        });
    }

    public async Task<OperationDataResult<EformAuthorizeResult>> RefreshToken()
    {
        var user = await userService.GetByIdAsync(userService.UserId);
        if (user == null)
            return new OperationDataResult<EformAuthorizeResult>(false,
                $"User with id {userService.UserId} not found");

        var token = await GenerateToken(user);
        var roleList = await userManager.GetRolesAsync(user);
        if (!roleList.Any())
        {
            return new OperationDataResult<EformAuthorizeResult>(false,
                $"Role for user {userService.UserId} not found");
        }

        return new OperationDataResult<EformAuthorizeResult>(true, new EformAuthorizeResult
        {
            Id = user.Id,
            AccessToken = token.token,
            UserName = user.UserName,
            Role = roleList.FirstOrDefault(),
            ExpiresIn = token.expireIn,
            FirstName = user.FirstName,
            LastName = user.LastName
        });
    }

    public async Task<(string token, DateTime expireIn)> GenerateToken(EformUser user)
    {
        if (user != null)
        {
            var timeStamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(AuthConsts.ClaimLastUpdateKey, timeStamp.ToString())
            };

            if (!string.IsNullOrEmpty(user.Locale))
            {
                claims.Add(new Claim("locale", user.Locale));
            }

            // Add user and roles claims
            var userClaims = userManager.GetClaimsAsync(user).Result;
            var userRoles = userManager.GetRolesAsync(user).Result;
            claims.AddRange(userClaims);
            foreach (var userRole in userRoles)
            {
                claims.Add(new Claim(ClaimTypes.Role, userRole));
                var role = roleManager.FindByNameAsync(userRole).Result;
                if (role != null)
                {
                    var roleClaims = roleManager.GetClaimsAsync(role).Result;
                    foreach (var roleClaim in roleClaims)
                    {
                        claims.Add(roleClaim);
                    }
                }
            }

            var userInMemoryClaims = await claimsService.GetUserPermissions(
                user.Id,
                userRoles.Contains(EformRole.Admin));

            // Add to memory
            var authItem = new AuthItem
            {
                TimeStamp = timeStamp,
                Claims = userInMemoryClaims
            };

            authCacheService.Set(authItem, user.Id);

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenOptions.Value.SigningKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expireIn = DateTime.Now.AddHours(24);
            var token = new JwtSecurityToken(tokenOptions.Value.Issuer,
                tokenOptions.Value.Issuer,
                claims.ToArray(),
                expires: expireIn,
                signingCredentials: credentials);

            return (new JwtSecurityTokenHandler().WriteToken(token), expireIn);
        }

        return (null, DateTime.Now);
    }


    public OperationDataResult<Dictionary<string, string>> GetCurrentUserClaims()
    {
        var result = new Dictionary<string, string>();
        var userId = userService.UserId;
        if (userId < 1)
        {
            throw new Exception("Current user not found!");
        }

        var auth = authCacheService.TryGetValue(userService.UserId);

        if (auth == null)
        {
            // since auth is not found return unauthorized
            throw new Exception("Current user not found!");

            //return new OperationDataResult<Dictionary<string, string>>(true, result);
        }

        foreach (var authClaim in auth.Claims)
        {
            result.Add(authClaim.Type, authClaim.Value);
        }

        return new OperationDataResult<Dictionary<string, string>>(true, result);
    }

    public OperationResult LogOut()
    {
        try
        {
            authCacheService.Remove(userService.UserId);
            return new OperationResult(true);
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            logger.LogError(e.Message);
            logger.LogTrace(e.StackTrace);
            return new OperationResult(false, e.Message);
        }
    }

    public OperationDataResult<bool> TwoFactorAuthForceInfo()
    {
        try
        {
            return new OperationDataResult<bool>(true, appSettings.Value.IsTwoFactorForced);
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            logger.LogError(e.Message);
            logger.LogTrace(e.StackTrace);
            return new OperationDataResult<bool>(false);
        }
    }

    public async Task<OperationDataResult<GoogleAuthInfoModel>> GetGoogleAuthenticatorInfo()
    {
        try
        {
            var user = await userService.GetCurrentUserAsync();
            if (user != null)
            {
                var model = new GoogleAuthInfoModel()
                {
                    PSK = user.GoogleAuthenticatorSecretKey,
                    IsTwoFactorEnabled = user.TwoFactorEnabled,
                    IsTwoFactorForced = appSettings.Value.IsTwoFactorForced
                };
                return new OperationDataResult<GoogleAuthInfoModel>(true, model);
            }
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            logger.LogError(e.Message);
            logger.LogTrace(e.StackTrace);
            return new OperationDataResult<GoogleAuthInfoModel>(false);
        }

        return new OperationDataResult<GoogleAuthInfoModel>(false);
    }

    public async Task<OperationResult> UpdateGoogleAuthenticatorInfo(GoogleAuthInfoModel requestModel)
    {
        try
        {
            var user = await userService.GetCurrentUserAsync();
            if (user != null)
            {
                user.TwoFactorEnabled = requestModel.IsTwoFactorEnabled;
                var updateResult = userManager.UpdateAsync(user).Result;
                if (updateResult.Succeeded)
                {
                    return new OperationResult(true);
                }
            }
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            logger.LogError(e.Message);
            logger.LogTrace(e.StackTrace);
            return new OperationResult(false);
        }

        return new OperationResult(false);
    }

    public async Task<OperationResult> DeleteGoogleAuthenticatorInfo()
    {
        try
        {
            var user = await userService.GetCurrentUserAsync();
            if (user != null)
            {
                user.GoogleAuthenticatorSecretKey = null;
                user.IsGoogleAuthenticatorEnabled = false;
                var updateResult = userManager.UpdateAsync(user).Result;
                if (updateResult.Succeeded)
                {
                    return new OperationResult(true);
                }
            }
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            logger.LogError(e.Message);
            logger.LogTrace(e.StackTrace);
            return new OperationResult(false);
        }

        return new OperationResult(false);
    }

    public async Task<OperationDataResult<GoogleAuthenticatorModel>> GetGoogleAuthenticator(LoginModel loginModel)
    {
        // try to sign in with user credentials
        var user = await userManager.FindByNameAsync(loginModel.Username);
        if (user == null)
        {
            return new OperationDataResult<GoogleAuthenticatorModel>(false,
                localizationService.GetString("UserNameOrPasswordIncorrect"));
        }

        var signInResult =
            await signInManager.CheckPasswordSignInAsync(user, loginModel.Password, true);

        if (!signInResult.Succeeded)
        {
            if (signInResult.IsLockedOut)
            {
                return new OperationDataResult<GoogleAuthenticatorModel>(false,
                    "Locked Out. Please, try again after 10 min");
            }

            // Credentials are invalid, or account doesn't exist
            return new OperationDataResult<GoogleAuthenticatorModel>(false,
                localizationService.GetString("UserNameOrPasswordIncorrect"));
        }

        // check if two factor is enabled
        var isTwoFactorAuthForced = appSettings.Value.IsTwoFactorForced;
        if (!user.TwoFactorEnabled && !isTwoFactorAuthForced)
        {
            return new OperationDataResult<GoogleAuthenticatorModel>(true);
        }

        // generate PSK and barcode
        if (!string.IsNullOrEmpty(user.GoogleAuthenticatorSecretKey) && user.IsGoogleAuthenticatorEnabled)
        {
            return new OperationDataResult<GoogleAuthenticatorModel>(true, new GoogleAuthenticatorModel());
        }

        var psk = KeyGeneration.GenerateRandomKey(20);
        var barcodeUrl = KeyUrl.GetTotpUrl(psk, user.UserName) + "&issuer=EformApplication";
        var model = new GoogleAuthenticatorModel
        {
            PSK = Base32.ToBase32String(psk),
            BarcodeUrl = HttpUtility.UrlEncode(barcodeUrl)
        };
        // write PSK to the user entity
        user.GoogleAuthenticatorSecretKey = model.PSK;
        var updateResult = userManager.UpdateAsync(user).Result;
        if (!updateResult.Succeeded)
        {
            return new OperationDataResult<GoogleAuthenticatorModel>(false,
                localizationService.GetString("ErrorWhileUpdatingPSK"));
        }

        // return
        return new OperationDataResult<GoogleAuthenticatorModel>(true, model);
    }
}