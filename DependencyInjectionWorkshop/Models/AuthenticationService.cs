﻿#region

using System;
using System.Net.Http;

#endregion

namespace DependencyInjectionWorkshop.Models
{
    public class AuthenticationService
    {
        private readonly FailedCounterProxy _failedCounterProxy;
        private readonly NLogAdapter _nLogAdapter;
        private readonly OtpProxy _otpProxy;
        private readonly ProfileDao _profileDao;
        private readonly Sha256Adapter _sha256Adapter;
        private readonly SlackAdapter _slackAdapter;

        public AuthenticationService()
        {
            _profileDao = new ProfileDao();
            _sha256Adapter = new Sha256Adapter();
            _otpProxy = new OtpProxy();
            _slackAdapter = new SlackAdapter();
            _failedCounterProxy = new FailedCounterProxy();
            _nLogAdapter = new NLogAdapter();
        }

        public bool Verify(string accountId, string inputPassword, string inputOtp)
        {
            var httpClient = new HttpClient() { BaseAddress = new Uri("http://joey.com/") };

            var isAccountLocked = _failedCounterProxy.IsAccountLocked(accountId, httpClient);
            if (isAccountLocked)
            {
                throw new FailedTooManyTimesException() { AccountId = accountId };
            }

            var passwordFromDb = _profileDao.GetPasswordFromDb(accountId);
            var hashedPassword = _sha256Adapter.GetHashedPassword(inputPassword);
            var currentOtp = _otpProxy.GetCurrentOtp(accountId, httpClient);

            if (passwordFromDb == hashedPassword && inputOtp == currentOtp)
            {
                _failedCounterProxy.ResetFailedCount(accountId, httpClient);
                return true;
            }
            else
            {
                _failedCounterProxy.AddFailedCount(accountId, httpClient);

                var failedCount = _failedCounterProxy.GetFailedCount(accountId, httpClient);
                _nLogAdapter.LogInfo($"accountId:{accountId} failed times:{failedCount}");

                _slackAdapter.Notify(accountId);
                return false;
            }
        }
    }

    public class FailedTooManyTimesException : Exception
    {
        public string AccountId { get; set; }
    }
}