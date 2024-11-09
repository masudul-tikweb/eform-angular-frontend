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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using eFormAPI.Web.Abstractions;
using eFormAPI.Web.Abstractions.Advanced;
using eFormAPI.Web.Infrastructure.Models.Units;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microting.eForm.Dto;
using Microting.eForm.Infrastructure.Constants;
using Microting.eFormApi.BasePn.Abstractions;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using Sentry;

namespace eFormAPI.Web.Services;

public class UnitsService(
    ILocalizationService localizationService,
    IEFormCoreService coreHelper,
    ILogger<UnitsService> logger)
    : IUnitsService
{
    public async Task<OperationDataResult<List<UnitModel>>> Index()
    {
        var core = await coreHelper.GetCore();
        await using var dbContext = core.DbContextHelper.GetDbContext();
        var units = await dbContext.Units.AsNoTracking()
            .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
            .Select(t => new UnitModel()
            {
                Id = t.Id,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                CustomerNo = t.CustomerNo,
                OtpCode = t.OtpCode,
                SiteId = (int)t.SiteId,
                SiteMicrotingUid = (int)t.Site.MicrotingUid,
                SiteName = t.Site.Name,
                Model = t.Model,
                Manufacturer = t.Manufacturer,
                Note = t.Note,
                Os = "",
                OsVersion = t.OsVersion,
                MicrotingUid = (int)t.MicrotingUid,
                eFormVersion = "0.0.0",
                InSightVersion = "0.0.0",
                eFormVersionHealth = "",
                InSightVersionHealth = "",
                PushEnabled = t.PushEnabled,
                SyncDialog = t.SyncDialog,
                SyncDefaultDelay = t.SyncDefaultDelay,
                SyncDelayEnabled = t.SyncDelayEnabled,
                SyncDelayPrCheckList = t.SyncDelayPrCheckList,
                IsLocked = t.IsLocked
            }).ToListAsync().ConfigureAwait(false);

        return new OperationDataResult<List<UnitModel>>(true, units);
    }

    public async Task<OperationResult> Create(UnitModel model)
    {
        try
        {
            var core = await coreHelper.GetCore();

            if (await core.Advanced_UnitCreate(model.SiteId).ConfigureAwait(false))
            {
                return new OperationResult(true, localizationService.GetString("UnitWasSuccessfullyCreated"));
            }

            return new OperationResult(false, localizationService.GetString("ErrorWhileCreatingUnit"));
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            logger.LogError(e.Message);
            logger.LogTrace(e.StackTrace);
            return new OperationResult(false, localizationService.GetString("ErrorWhileCreatingUnit"));
        }
    }

    public async Task<OperationResult> Update(UnitModel model)
    {
        try
        {
            var core = await coreHelper.GetCore();

            if (await core.Advanced_UnitMove(model.Id, model.SiteId).ConfigureAwait(false))
            {
                return new OperationResult(true, localizationService.GetString("UnitWasSuccessfullyCreated"));
            }

            return new OperationResult(false, localizationService.GetString("ErrorWhileCreatingUnit"));

        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            logger.LogError(e.Message);
            logger.LogTrace(e.StackTrace);
            return new OperationResult(false, localizationService.GetString("ErrorWhileCreatingUnit"));
        }
    }

    public async Task<OperationDataResult<UnitDto>> RequestOtp(int id)
    {
        try
        {
            var core = await coreHelper.GetCore();
            var unitDto = await core.Advanced_UnitRequestOtp(id);
            return new OperationDataResult<UnitDto>(true, localizationService.GetString("NewOTPCreatedSuccessfully"),
                unitDto);
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            logger.LogError(e.Message);
            logger.LogTrace(e.StackTrace);
            return new OperationDataResult<UnitDto>(false,
                localizationService.GetStringWithFormat("UnitParamOTPCouldNotCompleted", id));
        }
    }
}