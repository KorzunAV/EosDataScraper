using System;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace EosDataScraper.Api
{
    public class JwtSettings
    {
        public DateTimeOffset Now { get; }

        public string Issuer { get; set; }

        public string Key { get; set; }

        public string Audience { get; set; }

        public TimeSpan LifeTime { get; set; }

        private DateTimeOffset? _expires;
        public DateTimeOffset Expires
        {
            get
            {
                if (!_expires.HasValue)
                    _expires = Now.AddMinutes(LifeTime.TotalMinutes);
                return _expires.Value;
            }
        }

        public int MaxIncorrectPasswordCount { get; set; }

        public TimeSpan IncorrectPasswordLockTime { get; set; }

        public JwtSettings(DateTimeOffset utcNow)
        {
            Now = utcNow;
        }

        public SecurityKey GetSymmetricSecurityKey()
        {
            return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key));
        }
    }
}