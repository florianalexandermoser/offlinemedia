﻿using System;
using System.Collections.Generic;
using System.Text;
using OfflineMedia.Business.Services;

namespace OfflineMedia.Services.Mock
{
    public class DesignDispatchService : IDispatchService
    {
        public void CheckBeginInvokeOnUI(Action action)
        {
            action.Invoke();
        }
    }
}
