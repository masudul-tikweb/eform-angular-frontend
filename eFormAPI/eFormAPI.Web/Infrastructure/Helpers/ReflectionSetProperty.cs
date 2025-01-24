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

namespace eFormAPI.Web.Infrastructure.Helpers;

using System.Collections;
using System.Linq;

public static class ReflectionSetProperty
{
    /// <summary>
    /// Set property by path.
    /// </summary>
    /// <param name="target">object for set</param>
    /// <param name="property">path to property. must be separated by dots. for example:
    /// target = {x:{list:[1,2,3]}}; path = 'x.list.1'; setTo = 2; returns {x:{list:[2,2,3]}}</param>
    /// <param name="setTo">the value to set for path</param>
    public static void SetProperty(object target, string property, object setTo)
    {
        try
        {
            var parts = property.Split('.');
            // if target object is List and target object no end target -
            // we need cast to IList and get value by index
            if (target.GetType().Namespace == "System.Collections.Generic" && parts.Length != 1)
            {
                var targetList = (IList)target;
                var value = targetList[int.Parse(parts.First())];
                SetProperty(value, string.Join(".", parts.Skip(1)), setTo);
            }
            else
            {
                var prop = target.GetType().GetProperty(parts[0]);
                if (parts.Length == 1)
                {
                    // last property
                    prop.SetValue(target, setTo, null);
                }
                else
                {
                    // Not at the end, go recursive
                    var value = prop.GetValue(target);
                    SetProperty(value, string.Join(".", parts.Skip(1)), setTo);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }

    }
}