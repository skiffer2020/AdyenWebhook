using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Adyen
{
    public class LoggerService
    {
        private ILogger<LoggerService> _logger;
        public LoggerService(ILogger<LoggerService> logger)
        {
            _logger = logger;
        }
    }
}
