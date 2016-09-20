﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;

namespace Microsoft.AspNetCore.Rewrite.Internal
{
    public class RewriteRule : Rule
    {
        private readonly TimeSpan _regexTimeout = TimeSpan.FromSeconds(1);
        public Regex InitialMatch { get; }
        public string Replacement { get; }
        public bool StopProcessing { get; }
        public PathString UrlPrefix { get; }
        public RewriteRule(string regex, string replacement, string urlPrefix, bool stopProcessing)
        {
            if (string.IsNullOrEmpty(regex))
            {
                throw new ArgumentNullException(nameof(regex));
            }

            if (string.IsNullOrEmpty(replacement))
            {
                throw new ArgumentNullException(nameof(replacement));
            }

            InitialMatch = new Regex(regex, RegexOptions.Compiled | RegexOptions.CultureInvariant, _regexTimeout);
            Replacement = replacement;
            StopProcessing = stopProcessing;
            UrlPrefix = urlPrefix;
        }

        public override void ApplyRule(RewriteContext context)
        {
            var path = context.HttpContext.Request.Path;
            Match initMatchResults;
            if (UrlPrefix.Value.Length == 1 | path.StartsWithSegments(UrlPrefix))
            {
                if (path == PathString.Empty)
                {
                    initMatchResults = InitialMatch.Match(path.ToString());
                }
                else
                {
                    initMatchResults = InitialMatch.Match(path.ToString().Substring(1));
                }
                if (initMatchResults.Success)
                {
                    var result = initMatchResults.Result(Replacement);
                    var request = context.HttpContext.Request;

                    if (StopProcessing)
                    {
                        context.Result = RuleTermination.StopRules;
                    }

                    if (string.IsNullOrEmpty(result))
                    {
                        result = "/";
                    }

                    if (result.IndexOf("://", StringComparison.Ordinal) >= 0)
                    {
                        string scheme;
                        HostString host;
                        PathString pathString;
                        QueryString query;
                        FragmentString fragment;
                        UriHelper.FromAbsolute(result, out scheme, out host, out pathString, out query, out fragment);

                        request.Scheme = scheme;
                        request.Host = host;
                        request.Path = pathString;
                        request.QueryString = query.Add(request.QueryString);
                    }
                    else
                    {
                        var split = result.IndexOf('?');
                        if (split >= 0)
                        {
                            var newPath = result.Substring(0, split);
                            if (newPath[0] == '/')
                            {
                                request.Path = PathString.FromUriComponent(newPath);
                            }
                            else
                            {
                                request.Path = PathString.FromUriComponent('/' + newPath);
                            }
                            request.QueryString = request.QueryString.Add(
                                QueryString.FromUriComponent(
                                    result.Substring(split)));
                        }
                        else
                        {
                            if (result[0] == '/')
                            {
                                request.Path = PathString.FromUriComponent(result);
                            }
                            else
                            {
                                request.Path = PathString.FromUriComponent('/' + result);
                            }
                        }
                    }
                }
            }
        }
    }
}