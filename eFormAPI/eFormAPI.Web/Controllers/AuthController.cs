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

using System;
using System.Threading.Tasks;
using eFormAPI.Web.Abstractions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microting.eFormApi.BasePn.Infrastructure.Helpers;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.eFormApi.BasePn.Infrastructure.Models.Auth;

namespace eFormAPI.Web.Controllers;

[Authorize]
public class AuthController(
    IAuthService authService,
    ILocalizationService localizationService)
    : Controller
{
    [HttpPost]
    [AllowAnonymous]
    [Route("api/auth/token")]
    public async Task<OperationResult> AuthenticateUser(LoginModel model)
    {
        Log.LogEvent("api/auth/token called");
        return await authService.AuthenticateUser(model);
    }

    [HttpGet]
    [Route("api/auth/token/refresh")]
    public async Task<OperationResult> RefreshToken()
    {
        return await authService.RefreshToken();
    }

    [HttpGet]
    [Route("api/auth/logout")]
    public OperationResult Logout()
    {
        return authService.LogOut();
    }

    [HttpGet]
    [Route("api/auth/claims")]
    public IActionResult GetCurrentUserClaims()
    {
        try
        {
            return Ok(authService.GetCurrentUserClaims());

        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return Forbid();
        }
    }

    [HttpGet]
    [AllowAnonymous]
    [Route("api/auth/two-factor-info")]
    public OperationDataResult<bool> TwoFactorAuthForceInfo()
    {
        return authService.TwoFactorAuthForceInfo();
    }

    [HttpGet]
    [Route("api/auth/google-auth-info")]
    public async Task<OperationDataResult<GoogleAuthInfoModel>> GetGoogleAuthenticatorInfo()
    {
        return await authService.GetGoogleAuthenticatorInfo();
    }

    [HttpPost]
    [Route("api/auth/google-auth-info")]
    public async Task<OperationResult> UpdateGoogleAuthenticatorInfo([FromBody] GoogleAuthInfoModel requestModel)
    {
        return await authService.UpdateGoogleAuthenticatorInfo(requestModel);
    }

    [HttpDelete]
    [Route("api/auth/google-auth-info")]
    public async Task<OperationResult> DeleteGoogleAuthenticatorInfo()
    {
        return await authService.DeleteGoogleAuthenticatorInfo();
    }

    /// <summary>
    /// Get secret key and barcode to enable GoogleAuthenticator for account
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    [AllowAnonymous]
    [Route("api/auth/google-auth-key")]
    public async Task<OperationDataResult<GoogleAuthenticatorModel>> GetGoogleAuthenticator(
        [FromBody] LoginModel loginModel)
    {
        // check model
        if (!ModelState.IsValid)
        {
            return new OperationDataResult<GoogleAuthenticatorModel>(false,
                localizationService.GetString("InvalidUserNameOrPassword"));
        }

        return await authService.GetGoogleAuthenticator(loginModel);
    }
}