/*
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

using Microsoft.Extensions.Logging;
using Sentry;

namespace eFormAPI.Web.Services;

using Microting.eFormApi.BasePn.Infrastructure.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abstractions;
using Abstractions.Advanced;
using Infrastructure.Models;
using Infrastructure.Models.DeviceUsers;
using Microsoft.EntityFrameworkCore;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Microting.eFormApi.BasePn.Infrastructure.Models.Common;

public class DeviceUsersService(
    ILocalizationService localizationService,
    IEFormCoreService coreHelper,
    ILogger<DeviceUsersService> logger)
    : IDeviceUsersService
{
    public async Task<OperationDataResult<List<DeviceUser>>> Index(DeviceUserSearchRequestModel requestModel)
    {
        try
        {
            var core = await coreHelper.GetCore();
            var sdkDbContext = core.DbContextHelper.GetDbContext();
            // var deviceUsers = new List<DeviceUser>();

            var sitesQuery = sdkDbContext.Sites
                .Include(x => x.Units)
                .Include(x => x.SiteWorkers)
                .ThenInclude(x => x.Worker)
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed);

            try
            {
                sitesQuery = QueryHelper.AddFilterAndSortToQuery(sitesQuery, requestModel, new List<string> { "Name" });
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            var deviceUsers = await sitesQuery
                .Select(x => new DeviceUser
                {
                    CustomerNo = x.Units
                        .Where(y => y.WorkflowState != Constants.WorkflowStates.Removed)
                        .Select(y => y.CustomerNo)
                        .FirstOrDefault(),
                    FirstName = x.SiteWorkers.FirstOrDefault(y => y.WorkflowState != Constants.WorkflowStates.Removed)
                        .Worker.FirstName,
                    LastName = x.SiteWorkers.FirstOrDefault(y => y.WorkflowState != Constants.WorkflowStates.Removed)
                        .Worker.LastName,
                    LanguageId = x.LanguageId,
                    OtpCode = x.Units
                        .Where(y => y.WorkflowState != Constants.WorkflowStates.Removed)
                        .Select(y => y.OtpCode)
                        .FirstOrDefault(),
                    SiteId = x.Id,
                    SiteUid = x.MicrotingUid,
                    SiteName = x.Name,
                    UnitId = x.Units
                        .Where(y => y.WorkflowState != Constants.WorkflowStates.Removed)
                        .Select(y => y.MicrotingUid)
                        .FirstOrDefault(),
                    WorkerUid = x.SiteWorkers.FirstOrDefault(y => y.WorkflowState != Constants.WorkflowStates.Removed)
                        .Worker.MicrotingUid,
                    Language = sdkDbContext.Languages.Where(y => y.Id == x.LanguageId).Select(y => y.Name)
                        .SingleOrDefault() ?? "Danish",
                    LanguageCode = sdkDbContext.Languages.Where(y => y.Id == x.LanguageId).Select(y => y.LanguageCode)
                        .SingleOrDefault() ?? "da",
                    IsLocked = x.IsLocked
                })
                .ToListAsync();

            return new OperationDataResult<List<DeviceUser>>(true, deviceUsers);
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            logger.LogError(e.Message);
            logger.LogTrace(e.StackTrace);
            return new OperationDataResult<List<DeviceUser>>(false,
                localizationService.GetStringWithFormat("ErrorWhileGetDeviceUsers") + " " + e.Message);
        }
    }

    public async Task<OperationDataResult<int>> Create(DeviceUserModel deviceUserModel)
    {
        var core = await coreHelper.GetCore();
        deviceUserModel.UserFirstName = deviceUserModel.UserFirstName.Trim();
        deviceUserModel.UserLastName = deviceUserModel.UserLastName.Trim();
        var siteName = deviceUserModel.UserFirstName + " " + deviceUserModel.UserLastName;
        await using var db = core.DbContextHelper.GetDbContext();


        try
        {
            var site = await db.Sites.SingleOrDefaultAsync(x => x.Name == deviceUserModel.UserFirstName + " " + deviceUserModel.UserLastName && x.WorkflowState != Constants.WorkflowStates.Removed);

            if (site != null)
            {
                return new OperationDataResult<int>(false,
                    localizationService.GetStringWithFormat("UserUserNameAlreadyExist", siteName));
            }

            var siteDto = await core.SiteCreate(siteName, deviceUserModel.UserFirstName, deviceUserModel.UserLastName,
                null, deviceUserModel.LanguageCode);

            var sdkDbContext = core.DbContextHelper.GetDbContext();
            var id = await sdkDbContext.Sites.Where(x => x.MicrotingUid == siteDto.SiteId).Select(x => x.Id)
                .FirstAsync();


            return siteDto != null
                ? new OperationDataResult<int>(true,
                    localizationService.GetStringWithFormat("DeviceUserParamCreatedSuccessfully", siteDto.SiteName),
                    id)
                : new OperationDataResult<int>(false, localizationService.GetString("DeviceUserCouldNotBeCreated"));
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            logger.LogError(e.Message);
            logger.LogTrace(e.StackTrace);
            try
            {
                if (e.InnerException.Message == "The remote server returned an error: (402) Payment Required.")
                {
                    return new OperationDataResult<int>(false,
                        localizationService.GetString("YouNeedToBuyMoreLicenses"));
                }
                else
                {
                    return new OperationDataResult<int>(false,
                        localizationService.GetString("DeviceUserCouldNotBeCreated"));
                }
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
                logger.LogError(ex.Message);
                logger.LogTrace(ex.StackTrace);
                return new OperationDataResult<int>(false,
                    localizationService.GetString("DeviceUserCouldNotBeCreated"));
            }
        }
    }

    public async Task<OperationDataResult<DeviceUser>> Read(int id)
    {
        var core = await coreHelper.GetCore();
        await using var db = core.DbContextHelper.GetDbContext();

        //var siteDto = await core.SiteRead(id);
        DeviceUser deviceUser = null;
        var site = await db.Sites.SingleOrDefaultAsync(x => x.Id == id);
        if (site == null)
            return null;

        var siteWorker = db.SiteWorkers.Where(x => x.SiteId == site.Id).ToList().First();
        var worker = await db.Workers.SingleAsync(x => x.Id == siteWorker.WorkerId);
        var units = db.Units.Where(x => x.SiteId == site.Id).ToList();

        if (units.Any() && worker != null)
        {
            var unit = units.First();
            var language = db.Languages.Single(x => x.Id == site.LanguageId);
            deviceUser = new DeviceUser
            {
                CustomerNo = unit.CustomerNo,
                FirstName = worker.FirstName,
                Language = language.Name,
                LanguageCode = language.LanguageCode,
                LanguageId = site.LanguageId,
                LastName = worker.LastName,
                OtpCode = unit.OtpCode,
                SiteId = site.Id,
                SiteName = site.Name,
                SiteUid = site.MicrotingUid,
                UnitId = unit.Id,
                UnitUid = unit.MicrotingUid
            };
            //return new SiteDto((int)site.MicrotingUid, site.Name, worker.FirstName, worker.LastName, (int)unit.CustomerNo, unit.OtpCode ?? 0, (int)unit.MicrotingUid, worker.MicrotingUid);
        }

        return deviceUser != null
            ? new OperationDataResult<DeviceUser>(true, deviceUser)
            : new OperationDataResult<DeviceUser>(false,
                localizationService.GetStringWithFormat("DeviceUserParamCouldNotBeEdited", id));
    }

    public async Task<OperationResult> Update(DeviceUserModel deviceUserModel)
    {
        deviceUserModel.UserFirstName = deviceUserModel.UserFirstName.Trim();
        deviceUserModel.UserLastName = deviceUserModel.UserLastName.Trim();
        try
        {
            var core = await coreHelper.GetCore();
            await using var db = core.DbContextHelper.GetDbContext();
            var language = db.Languages.Single(x => x.LanguageCode == deviceUserModel.LanguageCode);
            var siteDto = await core.SiteRead(deviceUserModel.Id);
            if (siteDto.WorkerUid != null)
            {
                var workerDto = await core.Advanced_WorkerRead((int)siteDto.WorkerUid);
                if (workerDto != null)
                {
                    var fullName = deviceUserModel.UserFirstName + " " + deviceUserModel.UserLastName;
                    var isUpdated = await core.SiteUpdate(deviceUserModel.Id, fullName, deviceUserModel.UserFirstName,
                        deviceUserModel.UserLastName, workerDto.Email, deviceUserModel.LanguageCode);

                    // if (isUpdated)
                    // {
                    //     Site site = await db.Sites.SingleAsync(x => x.MicrotingUid == deviceUserModel.Id);
                    //     site.LanguageId = language.Id;
                    //     await site.Update(db);
                    // }
                    return isUpdated
                        ? new OperationResult(true, localizationService.GetString("DeviceUserUpdatedSuccessfully"))
                        : new OperationResult(false,
                            localizationService.GetStringWithFormat("DeviceUserParamCouldNotBeUpdated",
                                deviceUserModel.Id));
                }

                return new OperationResult(false, localizationService.GetString("DeviceUserCouldNotBeObtained"));
            }

            return new OperationResult(false, localizationService.GetString("DeviceUserNotFound"));
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            logger.LogError(e.Message);
            logger.LogTrace(e.StackTrace);
            return new OperationResult(false, localizationService.GetString("DeviceUserCouldNotBeUpdated"));
        }
    }

    public async Task<OperationResult> Delete(int id)
    {
        try
        {
            var core = await coreHelper.GetCore();
            var siteNameDto = await core.Advanced_SiteItemRead(id);

            return await core.SiteDelete(siteNameDto.SiteUId)
                ? new OperationResult(true,
                    localizationService.GetStringWithFormat("DeviceUserParamDeletedSuccessfully",
                        siteNameDto.SiteName))
                : new OperationResult(false,
                    localizationService.GetStringWithFormat("DeviceUserParamCouldNotBeDeleted", siteNameDto.SiteName));
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            logger.LogError(e.Message);
            logger.LogTrace(e.StackTrace);
            return new OperationResult(false,
                localizationService.GetStringWithFormat("DeviceUserParamCouldNotBeDeleted", id));
        }
    }

    public async Task<OperationDataResult<List<CommonDictionaryModel>>> ReadCommonDictionary()
    {
        try
        {
            var core = await coreHelper.GetCore();
            var sdkDbContext = core.DbContextHelper.GetDbContext();

            var sitesQuery = sdkDbContext.Sites
                .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed);

            var deviceUsers = await sitesQuery
                .Select(x => new CommonDictionaryModel
                {
                    Id = x.Id,
                    Name = x.Name
                })
                .ToListAsync();

            return new OperationDataResult<List<CommonDictionaryModel>>(true, deviceUsers);
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            logger.LogError(e.Message);
            logger.LogTrace(e.StackTrace);
            return new OperationDataResult<List<CommonDictionaryModel>>(false,
                localizationService.GetStringWithFormat("ErrorWhileGetDeviceUsers") + " " + e.Message);
        }
    }
}