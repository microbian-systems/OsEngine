﻿/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms.Integration;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Optimizer;
using OsEngine.Market.Servers.Tester;
using OsEngine.OsTrader.Panels;
using OsEngine.Robots;
using OsEngine.OsTrader.Panels.Tab.Internal;
using System.Threading.Tasks;
using System.Threading;
using System.Globalization;

namespace OsEngine.OsOptimizer
{
    public class OptimizerMaster
    {
        #region Service

        public OptimizerMaster()
        {
            _log = new Log("OptimizerLog", StartProgram.IsTester);
            _log.Listen(this);

            _threadsCount = 1;
            _startDeposit = 100000;

            Storage = new OptimizerDataStorage("Prime",true);
            Storage.SecuritiesChangeEvent += _storage_SecuritiesChangeEvent;
            Storage.TimeChangeEvent += _storage_TimeChangeEvent;

            _filterProfitValue = 10;
            _filterProfitIsOn = false;
            _filterMaxDrawDownValue = -10;
            _filterMaxDrawDownIsOn = false;
            _filterMiddleProfitValue = 0.001m;
            _filterMiddleProfitIsOn = false;
            _filterProfitFactorValue = 1;
            _filterProfitFactorIsOn = false;

            _percentOnFiltration = 30;

            Load();

            ManualControl = new BotManualControl("OptimizerManualControl", null, StartProgram.IsOsTrader);

            _optimizerExecutor = new OptimizerExecutor(this);
            _optimizerExecutor.LogMessageEvent += SendLogMessage;
            _optimizerExecutor.TestingProgressChangeEvent += _optimizerExecutor_TestingProgressChangeEvent;
            _optimizerExecutor.PrimeProgressChangeEvent += _optimizerExecutor_PrimeProgressChangeEvent;
            _optimizerExecutor.TestReadyEvent += _optimizerExecutor_TestReadyEvent;
            _optimizerExecutor.NeedToMoveUiToEvent += _optimizerExecutor_NeedToMoveUiToEvent;
            _optimizerExecutor.TimeToEndChangeEvent += _optimizerExecutor_TimeToEndChangeEvent;
            ProgressBarStatuses = new List<ProgressBarStatus>();
            PrimeProgressBarStatus = new ProgressBarStatus();
        }

        public int GetMaxBotsCount()
        {
            if(_parameters == null ||
                _parametersOn == null )
            {
                return 0;
            }

            int value = _optimizerExecutor.BotCountOneFaze(_parameters, _parametersOn) * IterationCount * 2;

            if(LastInSample)
            {
                value = value - _optimizerExecutor.BotCountOneFaze(_parameters, _parametersOn);
            }

            return value;
        }

        private void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\OptimizerSettings.txt", false)
                    )
                {
                    writer.WriteLine(ThreadsCount);
                    writer.WriteLine(StrategyName);
                    writer.WriteLine(StartDeposit);

                    writer.WriteLine(_filterProfitValue);
                    writer.WriteLine(_filterProfitIsOn);
                    writer.WriteLine(_filterMaxDrawDownValue);
                    writer.WriteLine(_filterMaxDrawDownIsOn);
                    writer.WriteLine(_filterMiddleProfitValue);
                    writer.WriteLine(_filterMiddleProfitIsOn);
                    writer.WriteLine(_filterProfitFactorValue);
                    writer.WriteLine(_filterProfitFactorIsOn);

                    writer.WriteLine(_timeStart.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine(_timeEnd.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine(_percentOnFiltration);

                    writer.WriteLine(_filterDealsCountValue);
                    writer.WriteLine(_filterDealsCountIsOn);
                    writer.WriteLine(_isScript);
                    writer.WriteLine(_iterationCount);
                    writer.WriteLine(_commissionType);
                    writer.WriteLine(_commissionValue);
                    writer.WriteLine(_lastInSample);

                    writer.Close();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void Load()
        {
            if (!File.Exists(@"Engine\OptimizerSettings.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\OptimizerSettings.txt"))
                {
                    _threadsCount = Convert.ToInt32(reader.ReadLine());
                    _strategyName = reader.ReadLine();
                    _startDeposit = reader.ReadLine().ToDecimal();
                    _filterProfitValue = reader.ReadLine().ToDecimal();
                    _filterProfitIsOn = Convert.ToBoolean(reader.ReadLine());
                    _filterMaxDrawDownValue = reader.ReadLine().ToDecimal();
                    _filterMaxDrawDownIsOn = Convert.ToBoolean(reader.ReadLine());
                    _filterMiddleProfitValue = reader.ReadLine().ToDecimal();
                    _filterMiddleProfitIsOn = Convert.ToBoolean(reader.ReadLine());
                    _filterProfitFactorValue = reader.ReadLine().ToDecimal();
                    _filterProfitFactorIsOn = Convert.ToBoolean(reader.ReadLine());

                    _timeStart = Convert.ToDateTime(reader.ReadLine(),CultureInfo.InvariantCulture);
                    _timeEnd = Convert.ToDateTime(reader.ReadLine(), CultureInfo.InvariantCulture);
                    _percentOnFiltration = reader.ReadLine().ToDecimal();

                    _filterDealsCountValue = Convert.ToInt32(reader.ReadLine());
                    _filterDealsCountIsOn = Convert.ToBoolean(reader.ReadLine());
                    _isScript = Convert.ToBoolean(reader.ReadLine());
                    _iterationCount = Convert.ToInt32(reader.ReadLine());
                    _commissionType = (ComissionType) Enum.Parse(typeof(ComissionType), 
                        reader.ReadLine() ?? ComissionType.None.ToString());
                    _commissionValue = reader.ReadLine().ToDecimal();
                    _lastInSample = Convert.ToBoolean(reader.ReadLine());

                    reader.Close();
                }
            }
            catch (Exception error)
            {
                //SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Progress of the optimization process

        private void _optimizerExecutor_PrimeProgressChangeEvent(int curVal, int maxVal)
        {
            if(PrimeProgressBarStatus.CurrentValue != curVal)
            {
                PrimeProgressBarStatus.CurrentValue = curVal;
            }

            if(PrimeProgressBarStatus.MaxValue != maxVal)
            {
                PrimeProgressBarStatus.MaxValue = maxVal;
            }
        }

        private void _optimizerExecutor_TestReadyEvent(List<OptimizerFazeReport> reports)
        {
            if(PrimeProgressBarStatus.CurrentValue != PrimeProgressBarStatus.MaxValue)
            {
                PrimeProgressBarStatus.CurrentValue = PrimeProgressBarStatus.MaxValue;
            }

            if (TestReadyEvent != null)
            {
                TestReadyEvent(reports);
            }
        }

        private void _optimizerExecutor_TimeToEndChangeEvent(TimeSpan timeToEnd)
        {
            if (TimeToEndChangeEvent != null)
            {
                TimeToEndChangeEvent(timeToEnd);
            }
        }

        public event Action<TimeSpan> TimeToEndChangeEvent;

        public event Action<List<OptimizerFazeReport>> TestReadyEvent;

        private void _optimizerExecutor_TestingProgressChangeEvent(int curVal, int maxVal, int numServer)
        {
            ProgressBarStatus status;
            try
            {
                status = ProgressBarStatuses.Find(st => st.Num == numServer);
            }
            catch
            {
                return;
            }
             
            if (status == null)
            {
                status = new ProgressBarStatus();
                status.Num = numServer;
                ProgressBarStatuses.Add(status);
            }

            status.CurrentValue = curVal;
            status.MaxValue = maxVal;
        }

        public List<ProgressBarStatus> ProgressBarStatuses;

        public ProgressBarStatus PrimeProgressBarStatus;

        #endregion

        #region Data store

        public bool ShowDataStorageDialog()
        {
            TesterSourceDataType storageSource = Storage.SourceDataType;
            string folder = Storage.PathToFolder;
            TesterDataType storageDataType = Storage.TypeTesterData;
            string setName = Storage.ActiveSet;

            Storage.ShowDialog();

            if(storageSource != Storage.SourceDataType
                || folder != Storage.PathToFolder 
                || storageDataType != Storage.TypeTesterData
                || setName != Storage.ActiveSet)
            {
                return true;
            }

            return false;
        }

        public OptimizerDataStorage Storage;

        private void _storage_TimeChangeEvent(DateTime timeStart, DateTime timeEnd)
        {
            TimeStart = timeStart;
            TimeEnd = timeEnd;
        }

        private void _storage_SecuritiesChangeEvent(List<Security> securities)
        {
            if (NewSecurityEvent != null)
            {
                NewSecurityEvent(securities);
            }

            TimeStart = Storage.TimeStart;
            TimeEnd = Storage.TimeEnd;
        }

        public event Action<List<Security>> NewSecurityEvent;

        #endregion

        #region Management

        public int ThreadsCount
        {
            get { return _threadsCount; }
            set
            {
                _threadsCount = value;
                Save();
            }
        }
        private int _threadsCount;

        public string StrategyName
        {
            get { return _strategyName; }
            set
            {
                _strategyName = value;
                TabsSimpleNamesAndTimeFrames = new List<TabSimpleEndTimeFrame>();
                TabsIndexNamesAndTimeFrames = new List<TabIndexEndTimeFrame>();
                Save();
            }
        }
        private string _strategyName;

        public bool IsScript
        {
            get { return _isScript; }
            set
            {
                _isScript = value;
                Save();
            }
        }
        private bool _isScript;

        public decimal StartDeposit
        {
            get { return _startDeposit; }
            set
            {
                _startDeposit = value;
                Save();
            }
        }
        private decimal _startDeposit;
        
        public ComissionType CommissionType
        {
            get => _commissionType;
            set
            {
                _commissionType = value;
                Save();
            }
        }
        private ComissionType _commissionType;      
        
        public decimal CommissionValue
        {
            get => _commissionValue;
            set
            {
                _commissionValue = value;
                Save();
            }
        }
        private decimal _commissionValue;

        public List<TabSimpleEndTimeFrame> TabsSimpleNamesAndTimeFrames;

        public List<TabIndexEndTimeFrame> TabsIndexNamesAndTimeFrames;

        public List<SecurityTester> SecurityTester
        {
            get { return Storage.SecuritiesTester; }
        }

        public BotManualControl ManualControl;

        public void ShowManualControlDialog()
        {
            ManualControl.ShowDialog();
        }

        #endregion

        #region Filters

        public decimal FilterProfitValue
        {
            get { return _filterProfitValue; }
            set
            {
                _filterProfitValue = value;
                Save();
            }
        }
        private decimal _filterProfitValue;

        public bool FilterProfitIsOn
        {
            get { return _filterProfitIsOn; }
            set
            {
                _filterProfitIsOn = value;
                Save();
            }
        }
        private bool _filterProfitIsOn;

        public decimal FilterMaxDrawDownValue
        {
            get { return _filterMaxDrawDownValue; }
            set
            {
                _filterMaxDrawDownValue = value;
                Save();
            }
        }
        private decimal _filterMaxDrawDownValue;

        public bool FilterMaxDrawDownIsOn
        {
            get { return _filterMaxDrawDownIsOn; }
            set
            {
                _filterMaxDrawDownIsOn = value;
                Save();
            }
        }
        private bool _filterMaxDrawDownIsOn;

        public decimal FilterMiddleProfitValue
        {
            get { return _filterMiddleProfitValue; }
            set
            {
                _filterMiddleProfitValue = value;
                Save();
            }
        }
        private decimal _filterMiddleProfitValue;

        public bool FilterMiddleProfitIsOn
        {
            get { return _filterMiddleProfitIsOn; }
            set
            {
                _filterMiddleProfitIsOn = value;
                Save();
            }
        }
        private bool _filterMiddleProfitIsOn;

        public decimal FilterProfitFactorValue
        {
            get { return _filterProfitFactorValue; }
            set
            {
                _filterProfitFactorValue = value;
                Save();
            }
        }
        private decimal _filterProfitFactorValue;

        public bool FilterProfitFactorIsOn
        {
            get { return _filterProfitFactorIsOn; }
            set
            {
                _filterProfitFactorIsOn = value;
                Save();
            }
        }
        private bool _filterProfitFactorIsOn;

        public int FilterDealsCountValue
        {
            get { return _filterDealsCountValue; }
            set
            {
                _filterDealsCountValue = value;
                Save();
            }
        }
        private int _filterDealsCountValue;

        public bool FilterDealsCountIsOn
        {
            get { return _filterDealsCountIsOn; }
            set
            {
                _filterDealsCountIsOn = value;
                Save();
            }
        }
        private bool _filterDealsCountIsOn;

        #endregion

        #region Optimization phases

        public bool IsAcceptedByFilter(OptimizerReport report)
        {
            if(report == null)
            {
                return false;
            }

            if (FilterMiddleProfitIsOn && report.AverageProfitPercentOneContract < FilterMiddleProfitValue)
            {
                return false;
            }

            if (FilterProfitIsOn && report.TotalProfit < FilterProfitValue)
            {
                return false;
            }

            if (FilterMaxDrawDownIsOn && report.MaxDrawDawn < FilterMaxDrawDownValue)
            {
                return false;
            }

            if (FilterProfitFactorIsOn && report.ProfitFactor < FilterProfitFactorValue)
            {
                return false;
            }

            if (FilterDealsCountIsOn && report.PositionsCount < FilterDealsCountValue)
            {
                return false;
            }

            return true;
        }

        public List<OptimizerFaze> Fazes;

        public DateTime TimeStart
        {
            get { return _timeStart; }
            set
            {
                _timeStart = value;
                Save();

                if (DateTimeStartEndChange != null)
                {
                    DateTimeStartEndChange();
                }
            }
        }
        private DateTime _timeStart;

        public DateTime TimeEnd
        {
            get { return _timeEnd; }
            set
            {
                _timeEnd = value;
                Save();
                if (DateTimeStartEndChange != null)
                {
                    DateTimeStartEndChange();
                }
            }
        }
        private DateTime _timeEnd;

        public decimal PercentOnFiltration
        {
            get { return _percentOnFiltration; }
            set
            {
                _percentOnFiltration = value;
                Save();
            }
        }
        private decimal _percentOnFiltration;

        public int IterationCount
        {
            get { return _iterationCount; }
            set
            {
                _iterationCount = value;
                Save();
            }
        }

        private int _iterationCount = 1;

        public bool LastInSample
        {
            get 
            { 
                return _lastInSample; 
            }
            set 
            {
                _lastInSample = value;
                Save();
            }
        }

        private bool _lastInSample;

        private decimal GetInSampleRecurs(decimal curLengthInSample,int fazeCount, bool lastInSample, int allDays)
        {
            // х = Y + Y/P * С;
            // x - общая длинна в днях. Уже известна
            // Y - длинна InSample
            // P - процент OutOfSample от InSample
            // C - количество отрезков

            decimal outOfSampleLength = curLengthInSample * (_percentOnFiltration / 100);

            int count = fazeCount;

            if(lastInSample)
            {
                count--;
            }

            int allLength = Convert.ToInt32(curLengthInSample + outOfSampleLength * count);

            if(allLength > allDays)
            {
                curLengthInSample--;
                return GetInSampleRecurs(curLengthInSample, fazeCount, lastInSample, allDays);
            }
            else
            {
                return curLengthInSample;
            }
        }

        public void ReloadFazes()
        {
            int fazeCount = IterationCount;

            if (fazeCount < 1)
            {
                fazeCount = 1;
            }

            if (TimeEnd == DateTime.MinValue ||
                TimeStart == DateTime.MinValue)
            {
                SendLogMessage(OsLocalization.Optimizer.Message12, LogMessageType.System);
                return;
            }

            int dayAll = Convert.ToInt32((TimeEnd - TimeStart).TotalDays);

            if (dayAll < 2)
            {
                SendLogMessage(OsLocalization.Optimizer.Message12, LogMessageType.System);
                return;
            }

            int daysOnInSample = (int)GetInSampleRecurs(dayAll, fazeCount, _lastInSample, dayAll);

            int daysOnForward = Convert.ToInt32(daysOnInSample * (_percentOnFiltration / 100));

            Fazes = new List<OptimizerFaze>();

            DateTime time = _timeStart;

            for (int i = 0; i < fazeCount; i++)
            {
                OptimizerFaze newFaze = new OptimizerFaze();
                newFaze.TypeFaze = OptimizerFazeType.InSample;
                newFaze.TimeStart = time;
                newFaze.TimeEnd = time.AddDays(daysOnInSample);
                time = time.AddDays(daysOnForward);
                newFaze.Days = daysOnInSample;
                Fazes.Add(newFaze);

                if(_lastInSample 
                    && i +1 == fazeCount)
                {
                    newFaze.Days = daysOnInSample;
                    break;
                }

                OptimizerFaze newFazeOut = new OptimizerFaze();
                newFazeOut.TypeFaze = OptimizerFazeType.OutOfSample;
                newFazeOut.TimeStart = newFaze.TimeStart.AddDays(daysOnInSample);
                newFazeOut.TimeEnd = newFazeOut.TimeStart.AddDays(daysOnForward);
                newFazeOut.TimeStart = newFazeOut.TimeStart.AddDays(1);
                newFazeOut.Days = daysOnForward;
                Fazes.Add(newFazeOut);
            }

            for(int i = 0;i < Fazes.Count;i++)
            {
                if(Fazes[i].Days <= 0)
                {
                    SendLogMessage(OsLocalization.Optimizer.Label50, LogMessageType.Error);
                    Fazes = new List<OptimizerFaze>();
                    return;
                }
            }


            /*while (DaysInFazes(Fazes) != dayAll)
            {
                int daysGone = DaysInFazes(Fazes) - dayAll;

                for (int i = 0; i < Fazes.Count && daysGone != 0; i++)
                {

                    if (daysGone > 0)
                    {
                        Fazes[i].Days--;
                        if (Fazes[i].TypeFaze == OptimizerFazeType.InSample &&
                            i + 1 != Fazes.Count)
                        {
                            Fazes[i + 1].TimeStart = Fazes[i + 1].TimeStart.AddDays(-1);
                        }
                        else
                        {
                            Fazes[i].TimeStart = Fazes[i].TimeStart.AddDays(-1);
                        }
                        daysGone--;
                    }
                    else if (daysGone < 0)
                    {
                        Fazes[i].Days++;
                        if (Fazes[i].TypeFaze == OptimizerFazeType.InSample && 
                            i + 1 != Fazes.Count)
                        {
                            Fazes[i + 1].TimeStart = Fazes[i + 1].TimeStart.AddDays(+1);
                        }
                        else
                        {
                            Fazes[i].TimeStart = Fazes[i].TimeStart.AddDays(+1);
                        }
                        daysGone++;
                    }
                }
            }*/
        }

        public event Action DateTimeStartEndChange;

        #endregion

        #region Optimization parameters

        public List<IIStrategyParameter> Parameters
        {
            get
            {
                if (string.IsNullOrEmpty(_strategyName))
                {
                    return null;
                }

                BotPanel bot = BotFactory.GetStrategyForName(_strategyName, "", StartProgram.IsOsOptimizer, _isScript);

                if (bot == null)
                {
                    return null;
                }

                if (bot.Parameters == null ||
                    bot.Parameters.Count == 0)
                {
                    return null;
                }

                if(_parameters != null)
                {
                    _parameters.Clear();
                    _parameters = null;
                }

                _parameters = new List<IIStrategyParameter>();

                for(int i = 0;i < bot.Parameters.Count;i++)
                {
                    _parameters.Add(bot.Parameters[i]);
                }
                
                for(int i = 0;i < _parameters.Count;i++)
                {
                    GetValueParameterSaveByUser(_parameters[i]);
                }

                bot.Delete();

                return _parameters;
            }
        }

        public List<IIStrategyParameter> ParametersStandard
        {
            get
            {
                if (string.IsNullOrEmpty(_strategyName))
                {
                    return null;
                }

                BotPanel bot = BotFactory.GetStrategyForName(_strategyName, "", StartProgram.IsOsOptimizer, _isScript);

                if (bot == null)
                {
                    return null;
                }

                if (bot.Parameters == null ||
                    bot.Parameters.Count == 0)
                {
                    return null;
                }

                if (_parameters != null)
                {
                    _parameters.Clear();
                    _parameters = null;
                }

                _parameters = new List<IIStrategyParameter>();

                for (int i = 0; i < bot.Parameters.Count; i++)
                {
                    _parameters.Add(bot.Parameters[i]);
                }

                return _parameters;
            }
        }

        private List<IIStrategyParameter> _parameters;

        private void GetValueParameterSaveByUser(IIStrategyParameter parameter)
        {
            if (!File.Exists(@"Engine\" + _strategyName + @"_StandartOptimizerParameters.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + _strategyName + @"_StandartOptimizerParameters.txt"))
                {
                    while (!reader.EndOfStream)
                    {
                        string[] save = reader.ReadLine().Split('#');

                        if (save[0] == parameter.Name)
                        {
                            parameter.LoadParamFromString(save);
                        }
                    }
                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        public void SaveStandardParameters()
        {
            if (_parameters == null ||
                _parameters.Count == 0)
            {
                return;
            }

            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + _strategyName + @"_StandartOptimizerParameters.txt", false)
                    )
                {
                    for(int i = 0;i < _parameters.Count;i++)
                    {
                        writer.WriteLine(_parameters[i].GetStringToSave());
                    }

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }

            SaveParametersOnOffByStrategy();
        }

        public List<bool> ParametersOn
        {
            get
            {

                _parametersOn = new List<bool>();
                for (int i = 0; _parameters != null && i < _parameters.Count; i++)
                {
                    _parametersOn.Add(false);
                }

                List<bool> paramsOnSaveBefore = GetParametersOnOffByStrategy();

                if(paramsOnSaveBefore != null && 
                    paramsOnSaveBefore.Count == _parametersOn.Count)
                {
                    _parametersOn = paramsOnSaveBefore;
                }

                return _parametersOn;
            }
        }
        private List<bool> _parametersOn;

        private List<bool> GetParametersOnOffByStrategy()
        {
            List<bool> result = new List<bool>();

            if (!File.Exists(@"Engine\" + _strategyName + @"_StandartOptimizerParametersOnOff.txt"))
            {
                return result;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + _strategyName + @"_StandartOptimizerParametersOnOff.txt"))
                {
                    while (!reader.EndOfStream)
                    {
                        result.Add(Convert.ToBoolean(reader.ReadLine()));
                    }
                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }

            return result;
        }

        private void SaveParametersOnOffByStrategy()
        {
            if (_parametersOn == null ||
               _parametersOn.Count == 0)
            {
                return;
            }

            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + _strategyName + @"_StandartOptimizerParametersOnOff.txt", false)
                    )
                {
                    for (int i = 0; i < _parametersOn.Count; i++)
                    {
                        writer.WriteLine(_parametersOn[i].ToString());
                    }

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }

        }

        #endregion

        #region Start optimization algorithm

        public OptimizerExecutor _optimizerExecutor;

        public bool Start()
        {
            if (CheckReadyData() == false)
            {
                return false;
            }

            if (_optimizerExecutor.Start(_parametersOn, _parameters))
            {
                ProgressBarStatuses = new List<ProgressBarStatus>();
                PrimeProgressBarStatus = new ProgressBarStatus();
            }
            return true;
        }

        public void Stop()
        {
            _optimizerExecutor.Stop();
        }

        private bool CheckReadyData()
        {
            if (Fazes == null || Fazes.Count == 0)
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Optimizer.Message14);
                ui.ShowDialog();
                SendLogMessage(OsLocalization.Optimizer.Message14, LogMessageType.System);
                if (NeedToMoveUiToEvent != null)
                {
                    NeedToMoveUiToEvent(NeedToMoveUiTo.Fazes);
                }
                return false;
            }

            if (TabsSimpleNamesAndTimeFrames == null ||
                TabsSimpleNamesAndTimeFrames.Count == 0)
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Optimizer.Message15);
                ui.ShowDialog();
                SendLogMessage(OsLocalization.Optimizer.Message15, LogMessageType.System);
                if (NeedToMoveUiToEvent != null)
                {
                    NeedToMoveUiToEvent(NeedToMoveUiTo.TabsAndTimeFrames);
                }
                return false;
            }

            if ((string.IsNullOrEmpty(Storage.ActiveSet) 
                && Storage.SourceDataType == TesterSourceDataType.Set)
                ||
                Storage.SecuritiesTester == null 
                ||
                Storage.SecuritiesTester.Count == 0)
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Optimizer.Message16);
                ui.ShowDialog();
                SendLogMessage(OsLocalization.Optimizer.Message16, LogMessageType.System);

                if (NeedToMoveUiToEvent != null)
                {
                    NeedToMoveUiToEvent(NeedToMoveUiTo.Storage);
                }
                return false;
            }

            if (string.IsNullOrEmpty(_strategyName))
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Optimizer.Message17);
                ui.ShowDialog();
                SendLogMessage(OsLocalization.Optimizer.Message17, LogMessageType.System);
                if (NeedToMoveUiToEvent != null)
                {
                    NeedToMoveUiToEvent(NeedToMoveUiTo.NameStrategy);
                }
                return false;
            }

            // проверяем наличие тайм-фрейма в обойме

            for (int i = 0; i < TabsSimpleNamesAndTimeFrames.Count; i++)
            {
                TabSimpleEndTimeFrame curFrame = TabsSimpleNamesAndTimeFrames[i];

                bool isInArray = false;

                for(int j = 0; j < Storage.SecuritiesTester.Count;j++)
                {
                    if (Storage.SecuritiesTester[j].Security.Name == curFrame.NameSecurity
                        && 
                        (Storage.SecuritiesTester[j].TimeFrame == curFrame.TimeFrame 
                        || Storage.SecuritiesTester[j].TimeFrame == TimeFrame.Sec1
                        || Storage.SecuritiesTester[j].TimeFrame == TimeFrame.Tick))
                    {
                        isInArray = true;
                    }
                }

                if(isInArray == false)
                {
                    CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Optimizer.Message43);
                    ui.ShowDialog();
                    SendLogMessage(OsLocalization.Optimizer.Message43, LogMessageType.System);

                    if (NeedToMoveUiToEvent != null)
                    {
                        NeedToMoveUiToEvent(NeedToMoveUiTo.NameStrategy);
                    }
                    return false;
                }
            }

            bool onParametersReady = false;

            for (int i = 0; i < _parametersOn.Count; i++)
            {
                if (_parametersOn[i])
                {
                    onParametersReady = true;
                    break;
                }
            }

            if (onParametersReady == false)
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Optimizer.Message18);
                ui.ShowDialog();
                SendLogMessage(OsLocalization.Optimizer.Message18, LogMessageType.System);
                if (NeedToMoveUiToEvent != null)
                {

                    NeedToMoveUiToEvent(NeedToMoveUiTo.Parameters);
                }
                return false;
            }


            // проверка наличия и состояния параметра Regime 
            bool onRgimeOff = false;

            for (int i = 0; i < _parameters.Count; i++)
            {
                if (_parameters[i].Name == "Regime" && _parameters[i].Type == StrategyParameterType.String)
                {
                    if (((StrategyParameterString)_parameters[i]).ValueString == "Off")
                    {
                        onRgimeOff = true;
                    }
                }

                else if (_parameters[i].Name == "Regime" && _parameters[i].Type == StrategyParameterType.CheckBox)
                {
                    if (((StrategyParameterCheckBox)_parameters[i]).CheckState == System.Windows.Forms.CheckState.Unchecked)
                    {
                        onRgimeOff = true;
                    }
                }
            }

            if (onRgimeOff == true)
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Optimizer.Message41);
                ui.ShowDialog();
                SendLogMessage(OsLocalization.Optimizer.Message41, LogMessageType.System);
                if (NeedToMoveUiToEvent != null)
                {
                    NeedToMoveUiToEvent(NeedToMoveUiTo.RegimeRow);
                }
                return false;
            }
            // Regime / конец

            return true;
        }

        private void _optimizerExecutor_NeedToMoveUiToEvent(NeedToMoveUiTo moveUiTo)
        {
            if (NeedToMoveUiToEvent != null)
            {
                NeedToMoveUiToEvent(moveUiTo);
            }
        }

        public event Action<NeedToMoveUiTo> NeedToMoveUiToEvent;

        #endregion

        #region One bot test

        public BotPanel TestBot(OptimizerFazeReport faze, OptimizerReport report)
        {
            if(_aloneTestIsOver == false)
            {
                return null;
            }

            _resultBotAloneTest = null;

            _aloneTestIsOver = false;

            _fazeToTestAloneTest = faze;
            _reportToTestAloneTest = report;
            _awaitUiMasterAloneTest = new AwaitObject(OsLocalization.Optimizer.Label52, 100, 0, true);

            Task.Run(RunAloneBotTest);

            AwaitUi ui = new AwaitUi(_awaitUiMasterAloneTest);
            ui.ShowDialog();

            Thread.Sleep(500);
           
            return _resultBotAloneTest;
        }

        private OptimizerFazeReport _fazeToTestAloneTest;

        private OptimizerReport _reportToTestAloneTest;

        private AwaitObject _awaitUiMasterAloneTest;

        private BotPanel _resultBotAloneTest;

        private bool _aloneTestIsOver = true;

        private async void RunAloneBotTest()
        {
            await Task.Delay(2000);
            _resultBotAloneTest = 
                _optimizerExecutor.TestBot(_fazeToTestAloneTest, _reportToTestAloneTest, 
                StartProgram.IsTester, _awaitUiMasterAloneTest);

            _aloneTestIsOver = true;
        }

        #endregion

        #region Log

        private Log _log;

        public void StartPaintLog(WindowsFormsHost logHost)
        {
            _log.StartPaint(logHost);
        }

        public void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }

    public class ProgressBarStatus
    {
        public int CurrentValue;

        public int MaxValue;

        public int Num;

        public bool IsFinalized;
    }

    public class OptimizerFaze
    {
        public OptimizerFazeType TypeFaze;

        public DateTime TimeStart
        {
            get { return _timeStart; }
            set
            {
                _timeStart = value;
                Days = Convert.ToInt32((TimeEnd - _timeStart).TotalDays);
            }
        }
        private DateTime _timeStart;

        public DateTime TimeEnd
        {
            get { return _timeEnd; }
            set
            {
                _timeEnd = value;
                Days = Convert.ToInt32((value - TimeStart).TotalDays);
            }
        }
        private DateTime _timeEnd;

        public int Days;

        public string GetSaveString()
        {
            string result = "";

            result += TypeFaze.ToString() + "%";

            result += _timeStart.ToString(CultureInfo.InvariantCulture) + "%";

            result += _timeEnd.ToString(CultureInfo.InvariantCulture) + "%";

            result += Days.ToString() + "%";

            return result;
        }

        public void LoadFromString(string saveStr)
        {
            string[] str = saveStr.Split('%');

            Enum.TryParse(str[0], out TypeFaze);

            _timeStart = Convert.ToDateTime(str[1], CultureInfo.InvariantCulture);

            _timeEnd = Convert.ToDateTime(str[2], CultureInfo.InvariantCulture);

            Days = Convert.ToInt32(str[3]);
        }

    }

    public enum OptimizerFazeType
    {
        InSample,

        OutOfSample
    }

    public class TabSimpleEndTimeFrame
    {
        public int NumberOfTab;

        public string NameSecurity;

        public TimeFrame TimeFrame;

        public string GetSaveString()
        {
            string result = "";
            result += NumberOfTab + "%";
            result += NameSecurity + "%";
            result += TimeFrame;

            return result;
        }

        public void SetFromString(string saveStr)
        {
            string[] str = saveStr.Split('%');

            NumberOfTab = Convert.ToInt32(str[0]);
            NameSecurity = str[1];
            Enum.TryParse(str[2], out TimeFrame);
        }
    }

    public class TabIndexEndTimeFrame
    {
        public int NumberOfTab;

        public List<string> NamesSecurity = new List<string>();

        public TimeFrame TimeFrame;

        public string Formula;

        public string GetSaveString()
        {
            string result = "";
            result += NumberOfTab + "%";
            result += TimeFrame + "%";
            result += Formula + "%";

            for (int i = 0;i < NamesSecurity.Count;i++)
            {
                result += NamesSecurity[i];

                if (i + 1 != NamesSecurity.Count)
                {
                    result += "^";
                }
            }

            return result;
        }

        public void SetFromString(string saveStr)
        {
            string[] str = saveStr.Split('%');

            NumberOfTab = Convert.ToInt32(str[0]);
            Enum.TryParse(str[1], out TimeFrame);
            Formula = str[2];

            if (str.Length > 2)
            {
                string[] secs = str[3].Split('^');

                for (int i = 0; i < secs.Length; i++)
                {
                    string sec = secs[i];
                    NamesSecurity.Add(sec);
                }
            }
        }
    }

    public enum NeedToMoveUiTo
    {
        NameStrategy,

        Fazes,

        Storage,

        TabsAndTimeFrames,

        Parameters,

        Filters,

        RegimeRow
    }
}