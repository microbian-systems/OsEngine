﻿using System;
using System.Collections.Generic;
using System.Text;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using System.Threading;
using OsEngine.Market.Servers.CoinEx.Futures.Entity;
using System.Security.Cryptography;
using WebSocketSharp;

namespace OsEngine.Market.Servers.CoinEx.Futures
{
    public class CoinExServerFutures : AServer
    {
        public CoinExServerFutures()
        {
            CoinExServerRealization realization = new CoinExServerRealization();
            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
            CreateParameterEnum("Market depth", "20", new List<string> { "5", "10", "20", "50" });
        }
    }

    public class CoinExServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection
        public CoinExServerRealization()
        {
            Thread worker = new Thread(ConnectionCheckThread);
            worker.Name = "CheckAliveCoinEx";
            worker.Start();

            Thread worker2 = new Thread(DataMessageReaderThread);
            worker2.Name = "DataMessageReaderCoinEx";
            worker2.Start();

        }

        public void Connect()
        {
            _securities.Clear();
            _portfolios.Clear();
            //_securitiesSubscriptions.Clear();
            _subscribledSecurities.Clear();
        }

        public ServerConnectStatus ServerStatus { get; set; }
        public void Dispose()
        {
            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;

                if (DisconnectEvent != null)
                {
                    DisconnectEvent();
                }
            }

            //DisposeSockets();

            _subscribledSecurities.Clear();
            _securities = new List<Security>();

            // Check rest auth
            if (!GetCurrentPortfolios())
            {
                SendLogMessage("Authorization Error. Probably an invalid keys are specified, check it!",
                    LogMessageType.Error);
            }

            // Temp
            ServerStatus = ServerConnectStatus.Connect;
            ConnectEvent();
        }

        public DateTime ServerTime { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public event Action<MarketDepth> MarketDepthEvent;
        public event Action<Trade> NewTradesEvent;
        public event Action<Order> MyOrderEvent;
        public event Action<MyTrade> MyTradeEvent;

        public void CancelAllOrders()
        {
            throw new NotImplementedException();
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            throw new NotImplementedException();
        }

        public void CancelOrder(Order order)
        {
            throw new NotImplementedException();
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
            throw new NotImplementedException();
        }

        public void GetAllActivOrders()
        {
            throw new NotImplementedException();
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            throw new NotImplementedException();
        }

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            throw new NotImplementedException();
        }

        public void GetOrderStatus(Order order)
        {
            throw new NotImplementedException();
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            throw new NotImplementedException();
        }

        public void SendOrder(Order order)
        {
            throw new NotImplementedException();
        }

        public void Subscrible(Security security)
        {
            throw new NotImplementedException();
        }
        #endregion

        #region 2 Properties
        private string _publicKey;

        private string _secretKey;

        // Spot or Margin
        private string _marketMode;
        // Market Depth
        private int _marketDepth;

        public ServerType ServerType
        {
            get { return ServerType.CoinExFutures; }
        }
        public event Action ConnectEvent;
        public event Action DisconnectEvent;



        public List<IServerParameter> ServerParameters { get; set; }

        private CoinExRestClient _restClient;
        private WebSocket _wsClient;

        // https://docs.coinex.com/api/v2/rate-limit

        private RateGate _rateGateSendOrder = new RateGate(30, TimeSpan.FromMilliseconds(950));

        private RateGate _rateGateCancelOrder = new RateGate(60, TimeSpan.FromMilliseconds(950));

        private RateGate _rateGateGetOrder = new RateGate(50, TimeSpan.FromMilliseconds(950));

        private RateGate _rateGateOrdersHistory = new RateGate(10, TimeSpan.FromMilliseconds(950));

        private RateGate _rateGateAccountStatus = new RateGate(10, TimeSpan.FromMilliseconds(950));
        #endregion

        #region 3 Securities
        private List<Security> _securities = new List<Security>();

        public void GetSecurities()
        {
            UpdateSec();

            if (_securities.Count > 0)
            {
                SendLogMessage("Securities loaded. Count: " + _securities.Count, LogMessageType.System);

                if (SecurityEvent != null)
                {
                    SecurityEvent.Invoke(_securities);
                }
            }

        }

        private void UpdateSec()
        {
            // https://docs.coinex.com/api/v2/spot/market/http/list-market

            string endPoint = "/spot/market";

            try
            {
                List<CexSecurity> securities = _restClient.Get<List<CexSecurity>>(endPoint).Result;
                UpdateSecuritiesFromServer(securities);
            }
            catch (Exception exception)
            {
                SendLogMessage("Securities request error: " + exception.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateSecuritiesFromServer(List<CexSecurity> stocks)
        {
            try
            {
                if (stocks == null ||
                    stocks.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < stocks.Count; i++)
                {
                    _securities.Add((Security)stocks[i]);
                }

                _securities.Sort(delegate (Security x, Security y)
                {
                    return String.Compare(x.NameFull, y.NameFull);
                });
            }
            catch (Exception e)
            {
                SendLogMessage($"Error loading stocks: {e.Message}" + e.ToString(), LogMessageType.Error);
            }
        }

        public event Action<List<Security>> SecurityEvent;
        #endregion

        #region 4 Portfolios
        public event Action<List<Portfolio>> PortfolioEvent;
        private List<Portfolio> _portfolios = new List<Portfolio>();
        private string PortfolioName = "CoinExFutures";

        public void GetPortfolios()
        {
            GetCurrentPortfolios();
        }

        public bool GetCurrentPortfolios()
        {
            _rateGateAccountStatus.WaitToProceed(); // FIX упорядочить запросы

            try
            {
                string endPoint = "/assets/futures/balance";
                List<CexPortfolioItem>? cexPortfolio = _restClient.Get<List<CexPortfolioItem>>(endPoint, true).Result;

                ConvertToPortfolio(cexPortfolio);
                return _portfolios.Count > 0;

            }
            catch (Exception exception)
            {
                SendLogMessage("Portfolio request error " + exception.ToString(), LogMessageType.Error);
            }
            return false;
        }

        private void ConvertToPortfolio(List<CexPortfolioItem> portfolioItems)
        {
            Portfolio portfolio = new Portfolio();
            portfolio.ServerType = ServerType;
            portfolio.Number = this.PortfolioName;
            portfolio.ValueBegin = 0;
            portfolio.ValueCurrent = 0;

            if (portfolioItems == null || portfolioItems.Count == 0)
            {
                SendLogMessage("No portfolios detected!", LogMessageType.Error);
            }
            else
            {

                for (int i = 0; i < portfolioItems.Count; i++)
                {
                    PositionOnBoard pos = (PositionOnBoard)portfolioItems[i];
                    pos.PortfolioName = this.PortfolioName;
                    pos.ValueBegin = pos.ValueCurrent;

                    portfolio.SetNewPosition(pos);
                }

                portfolio.ValueCurrent = getPortfolioValue(portfolio);
            }



            if (_portfolios.Count > 0)
            {
                _portfolios[0] = portfolio;
            }
            else
            {
                _portfolios.Add(portfolio);
            }


            if (PortfolioEvent != null && _portfolios.Count > 0)
            {
                PortfolioEvent(_portfolios);
            }
        }

        public decimal getPortfolioValue(Portfolio portfolio)
        {
            List<PositionOnBoard> poses = portfolio.GetPositionOnBoard();
            string mainCurrency = "";
            for (int i = 0; i < poses.Count; i++)
            {
                if (poses[i].SecurityNameCode == "USDT"
                 || poses[i].SecurityNameCode == "USDC"
                 || poses[i].SecurityNameCode == "USD"
                 || poses[i].SecurityNameCode == "RUB"
                 || poses[i].SecurityNameCode == "EUR")
                {
                    mainCurrency = poses[i].SecurityNameCode;
                    break;
                }
            }

            if (string.IsNullOrEmpty(mainCurrency)) { return 0; }

            List<string> securities = new List<string>();
            for (int i = 0; i < poses.Count; i++)
            {
                if (poses[i].SecurityNameCode == mainCurrency)
                {
                    continue;
                }
                securities.Add(poses[i].SecurityNameCode + mainCurrency);
            }

            List<CexMarketInfoItem> marketInfo = GetMarketsInfo(securities);

            decimal val = 0;
            for (int i = 0; i < poses.Count; i++)
            {
                if (poses[i].SecurityNameCode == mainCurrency)
                {
                    val += poses[i].ValueCurrent;
                    continue;
                }
                else
                {
                    if (marketInfo != null)
                    {
                        for (int j = 0; j < marketInfo.Count; j++)
                        {
                            if (marketInfo[j].market == poses[i].SecurityNameCode + mainCurrency)
                            {
                                val += poses[i].ValueCurrent * marketInfo[j].last.ToString().ToDecimal();
                                break;
                            }
                        }
                    }
                }
            }

            return Math.Round(val, 2);
        }

        public List<CexMarketInfoItem> GetMarketsInfo(List<string> securities)
        {
            // https://docs.coinex.com/api/v2/spot/market/http/list-market-ticker
            List<CexMarketInfoItem> cexInfo = new List<CexMarketInfoItem>();

            string endPoint = "/spot/ticker";
            try
            {
                if (securities.Count > 10)
                {
                    // Get all markets info
                    securities = new List<string>();
                }

                cexInfo = _restClient.Get<List<CexMarketInfoItem>>(endPoint, false, new Dictionary<string, object>()
                {
                    { "market", String.Join(",", securities.ToArray())},
                }).Result;


            }
            catch (Exception exception)
            {
                SendLogMessage("Market info request error:" + exception.ToString(), LogMessageType.Error);
            }
            return cexInfo;
        }
        #endregion

        #region 5 Data

        #endregion

        #region 6 WebSocket creation

        #endregion

        #region 7 WebSocket events

        #endregion

        #region 8 WebSocket check alive
        private void ConnectionCheckThread()
        {
            while (true)
            {

            }
        }
        #endregion

        #region 9 Security subscrible
        private List<Security> _subscribledSecurities = new List<Security>();
        #endregion

        #region 10 WebSocket parsing the messages
        private void DataMessageReaderThread()
        {
            while (true)
            {

            }
        }
        #endregion

        #region 11 Trade

        #endregion

        #region 12 Queries

        #endregion

        #region 13 Log
        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;
        #endregion

        #region 14 Helpers
        public static decimal GetPriceStep(int ScalePrice)
        {
            if (ScalePrice == 0)
            {
                return 1;
            }
            string priceStep = "0,";
            for (int i = 0; i < ScalePrice - 1; i++)
            {
                priceStep += "0";
            }

            priceStep += "1";

            return priceStep.ToString().ToDecimal();
        }

        private DateTime _lastTimeRestCheckConnection = DateTime.MinValue;
        private DateTime _lastTimeWsCheckConnection = DateTime.MinValue;
        private bool SendRestPing()
        {
            string endPoint = "/ping";
            try
            {
                CexRestResp pong = _restClient.Get<CexRestResp>(endPoint).Result;
                pong.EnsureSuccess();
            }
            catch (Exception ex)
            {
                // ex.InnerException.Message
                return false;
            }
            _lastTimeRestCheckConnection = DateTime.Now;

            return true;
        }
        private void SendWsPing()
        {
            CexRequestSocketPing message = new CexRequestSocketPing();
            _wsClient?.Send(message.ToString());
        }
        #endregion
    }

    #region 15 Signer
    public static class Signer
    {
        public static string Sign(string message, string secret)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
            {
                var r = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
                return BitConverter.ToString(r).Replace("-", "").ToLower();
            }
        }


        public static string RestSign(string method, string path, string body, long timestamp, string secret)
        {
            var message = method + path + body + timestamp.ToString();
            return Sign(message, secret);
        }
    }
    #endregion
}
