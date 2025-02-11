﻿namespace OsEngine.Market.Servers.Plaza
{
    public class PlazaServerPermission : IServerPermission
    {
        public ServerType ServerType
        {
            get { return ServerType.Plaza; }
        }

        #region DataFeedPermissions

        public bool DataFeedTf1SecondCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTf2SecondCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTf5SecondCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTf10SecondCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTf15SecondCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTf20SecondCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTf30SecondCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTfTickCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTfMarketDepthCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTf1MinuteCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTf2MinuteCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTf5MinuteCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTf10MinuteCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTf15MinuteCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTf30MinuteCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTf1HourCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTf2HourCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTf4HourCanLoad
        {
            get { return false; }
        }

        public bool DataFeedTfDayCanLoad
        {
            get { return false; }
        }

        #endregion

        #region Trade permission

        public bool MarketOrdersIsSupport
        {
            get { return true; }
        }

        public bool IsCanChangeOrderPrice
        {
            get { return true; }
        }

        public int WaitTimeSecondsAfterFirstStartToSendOrders
        {
            get { return 20; }
        }

        public bool IsUseLotToCalculateProfit
        {
            get { return true; }
        }

        public bool UseStandardCandlesStarter
        {
            get { return false; }
        }

        public TimeFramePermission TradeTimeFramePermission
        {
            get { return _tradeTimeFramePermission; }
        }
        private TimeFramePermission _tradeTimeFramePermission = new TimeFramePermission()
        {
            TimeFrameSec1IsOn = true,
            TimeFrameSec2IsOn = true,
            TimeFrameSec5IsOn = true,
            TimeFrameSec10IsOn = true,
            TimeFrameSec15IsOn = true,
            TimeFrameSec20IsOn = true,
            TimeFrameSec30IsOn = true,
            TimeFrameMin1IsOn = true,
            TimeFrameMin2IsOn = true,
            TimeFrameMin3IsOn = true,
            TimeFrameMin5IsOn = true,
            TimeFrameMin10IsOn = true,
            TimeFrameMin15IsOn = true,
            TimeFrameMin20IsOn = true,
            TimeFrameMin30IsOn = true,
            TimeFrameMin45IsOn = true,
            TimeFrameHour1IsOn = true,
            TimeFrameHour2IsOn = true,
            TimeFrameHour4IsOn = true,
            TimeFrameDayIsOn = true
        };


        public bool ManuallyClosePositionOnBoard_IsOn
        {
            get { return true; }
        }

        public string[] ManuallyClosePositionOnBoard_ValuesForTrimmingName
        {
            get
            {
                return null;
            }
        }

        public string[] ManuallyClosePositionOnBoard_ExceptionPositionNames
        {
            get
            {
                return null;
            }
        }

        public bool CanQueryOrdersAfterReconnect
        {
            get { return true; }
        }

        public bool CanQueryOrderStatus
        {
            get { return false; }
        }

        #endregion

        #region Other Permissions

        public bool IsNewsServer
        {
            get { return false; }
        }

        #endregion
    }
}
