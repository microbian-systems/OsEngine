﻿/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using OsEngine.Entity;
using OsEngine.Logging;
using System.Drawing;
using OsEngine.Language;
using OsEngine.Alerts;
using System.Globalization;

namespace OsEngine.OsTrader
{
    /// <summary>
    /// class responsible for drawing the global position of all robots in the main window
    /// класс отвечающий за прорисовку глобальной позиции всех роботов в главном окне
    /// </summary>
    public class GlobalPositionViewer
    {
        public GlobalPositionViewer(StartProgram startProgram)
        {
            _startProgram = startProgram;
            _currentCulture = OsLocalization.CurCulture;

            Task task = new Task(WatcherThreadWorkArea);
            task.Start();
        }

        CultureInfo _currentCulture;

        /// <summary>
        /// add another magazine to the collection to draw his deals
        /// добавить ещё один журнал в коллекцию для прорисовки его сделок
        /// </summary>
        /// <param name="journal">new journal / новый журнал</param>
        public void SetJournal(Journal.Journal journal)
        {
            try
            {
                if (_journals == null)
                {
                    _journals = new List<Journal.Journal>();
                }

                if(journal == null)
                {
                    return;
                }

                for(int i = 0;i < _journals.Count;i++)
                {
                    if(_journals[i].Name == journal.Name)
                    {
                        return;
                    }
                }

                _journals.Add(journal);

            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public Position GetPositionForNumber(int number)
        {
            List<Position> deals = new List<Position>();

            for(int i = 0;i < _journals.Count;i++)
            {
                if(_journals[i] != null)
                {
                    List<Position> curPoses = _journals[i].OpenPositions;

                    deals.AddRange(curPoses);
                }
            }

            return deals.Find(position => position.Number == number);
        }

        public Position GetClosePositionForNumber(int number)
        {
            List<Position> deals = new List<Position>();

            for (int i = 0; i < _journals.Count; i++)
            {
                if (_journals[i] != null)
                {
                    List<Position> curPoses = _journals[i].AllPosition;

                    deals.AddRange(curPoses);
                }
            }

            return deals.Find(position => position.Number == number);
        }

        /// <summary>
        /// clear previously loaded journals
        /// очистить от ранее загруженых журналов
        /// </summary>
        public void ClearJournals()
        {
            try
            {
                if (_gridOpenPoses.InvokeRequired)
                {
                    _gridOpenPoses.Invoke(new Action(ClearJournals));
                    return;
                }

                _journals = null;
                _gridOpenPoses.Rows.Clear();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// journals we follow
        /// журналы за которыми мы следим
        /// </summary>
        private List<Journal.Journal> _journals;

        public void Delete()
        {
            _journals = null;

            if(_hostOpenPoses != null)
            {
                _hostOpenPoses.Child = null;
                _hostOpenPoses = null;
            }

            if(_hostClosePoses != null)
            {
                _hostClosePoses.Child = null;
                _hostClosePoses = null;
            }

            if(_gridOpenPoses != null)
            {
                DataGridFactory.ClearLinks(_gridOpenPoses);
                _gridOpenPoses.Click -= _gridAllPositions_Click;
                _gridOpenPoses.DoubleClick -= _gridOpenPoses_DoubleClick;
                _gridOpenPoses = null;
            }

            if(_gridClosePoses != null)
            {
                DataGridFactory.ClearLinks(_gridClosePoses);
                _gridClosePoses.Click -= _gridClosePoses_Click;
                _gridClosePoses.DoubleClick -= _gridClosePoses_DoubleClick;
                _gridClosePoses = null;
            }
        }

        private WindowsFormsHost _hostOpenPoses;

        private DataGridView _gridOpenPoses;

        private WindowsFormsHost _hostClosePoses;

        private DataGridView _gridClosePoses;

        /// <summary>
        /// table for drawing positions
        /// программа запустившая класс
        /// </summary>
        private StartProgram _startProgram;

        //drawing / прорисовка

        /// <summary>
        /// stop drawing elements
        /// остановить прорисовку элементов 
        /// </summary>
        public void StopPaint()
        {
            try
            {
                if(_hostOpenPoses == null)
                {
                    return;
                }

                if (!_hostOpenPoses.CheckAccess())
                {
                    _hostOpenPoses.Dispatcher.Invoke(StopPaint);
                    return;
                }

                _hostOpenPoses.Child = null;

                if(_hostClosePoses != null)
                {
                    _hostClosePoses.Child = null;
                }

                if (_hostOpenPoses != null)
                {
                    _hostOpenPoses.Child = null;
                    _hostOpenPoses = null;
                }

                if (_hostOpenPoses != null)
                {
                    _hostClosePoses.Child = null;
                    _hostClosePoses = null;
                }

            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// start drawing elements
        /// запустить прорисовку элементов
        /// </summary>
        public void StartPaint(WindowsFormsHost openPositionHost, WindowsFormsHost closePositionHost)
        {
            try
            {
                _hostOpenPoses = openPositionHost;
                _hostClosePoses = closePositionHost;

                if(_hostOpenPoses == null ||
                    _hostClosePoses == null)
                {
                    return;
                }

                if (!_hostOpenPoses.CheckAccess())
                {
                    _hostOpenPoses.Dispatcher.Invoke(
                        new Action<WindowsFormsHost, WindowsFormsHost>(StartPaint),openPositionHost,closePositionHost);
                    return;
                }

                if(_gridOpenPoses == null)
                {
                    _gridOpenPoses = CreateNewTable();
                    _gridOpenPoses.Click += _gridAllPositions_Click;
                    _gridOpenPoses.DoubleClick += _gridOpenPoses_DoubleClick;
                }

                if (openPositionHost != null)
                {
                    _hostOpenPoses = openPositionHost;
                    _hostOpenPoses.Child = _gridOpenPoses;
                    _hostOpenPoses.Child.Show();
                }

                if (_gridClosePoses == null)
                {
                    _gridClosePoses = CreateNewTable();
                    _gridClosePoses.Click += _gridClosePoses_Click;
                    _gridClosePoses.DoubleClick += _gridClosePoses_DoubleClick;
                }

                if (closePositionHost != null)
                {
                    _hostClosePoses = closePositionHost;
                    _hostClosePoses.Child = _gridClosePoses;
                    _hostClosePoses.Child.Show();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// create a table
        /// создать таблицу
        /// </summary>
        private DataGridView CreateNewTable()
        {
            try
            {
                DataGridView newGrid = DataGridFactory.GetDataGridPosition();
                newGrid.ScrollBars = ScrollBars.Vertical;

                return newGrid;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        /// <summary>
        /// take a row for the table representing the position
        /// взять строку для таблицы представляющую позицию
        /// </summary>
        private DataGridViewRow GetRow(Position position)
        {
            if (position == null)
            {
                return null;
            }

            try
            {
                DataGridViewRow nRow = new DataGridViewRow();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[0].Value = position.Number;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[1].Value = position.TimeCreate.ToString(_currentCulture);

                nRow.Cells.Add(new DataGridViewTextBoxCell());

                if (position.TimeClose != position.TimeOpen)
                {
                    nRow.Cells[2].Value = position.TimeClose.ToString(_currentCulture);
                }
                else
                {
                    nRow.Cells[2].Value = "";
                }

                int decimalsPrice = position.PriceStep.ToStringWithNoEndZero().DecimalsCount();

                decimalsPrice++;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[3].Value = position.NameBot;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[4].Value = position.SecurityName;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[5].Value = position.Direction;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[6].Value = position.State;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[7].Value = position.MaxVolume.ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[8].Value = position.OpenVolume.ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[9].Value = position.WaitVolume.ToStringWithNoEndZero();

                decimal openPrice = position.EntryPrice;

                if (openPrice == 0)
                {
                    if (position.OpenOrders != null &&
                        position.OpenOrders.Count != 0 &&
                        position.State != PositionStateType.OpeningFail)
                    {
                        openPrice = position.OpenOrders[position.OpenOrders.Count - 1].Price;
                    }
                }

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[10].Value = Math.Round(openPrice, decimalsPrice).ToStringWithNoEndZero();

                decimal closePrice = position.ClosePrice;

                if (closePrice == 0)
                {
                    if (position.CloseOrders != null &&
                        position.CloseOrders.Count != 0 &&
                        position.State != PositionStateType.ClosingFail)
                    {
                        closePrice = position.CloseOrders[position.CloseOrders.Count - 1].Price;
                    }
                }

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[11].Value = Math.Round(closePrice, decimalsPrice).ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[12].Value = Math.Round(position.ProfitPortfolioPunkt, decimalsPrice).ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[13].Value = Math.Round(position.StopOrderRedLine, decimalsPrice).ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[14].Value = Math.Round(position.StopOrderPrice, decimalsPrice).ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[15].Value = Math.Round(position.ProfitOrderRedLine, decimalsPrice).ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[16].Value = Math.Round(position.ProfitOrderPrice, decimalsPrice).ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[17].Value = position.SignalTypeOpen;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[18].Value = position.SignalTypeClose;

                return nRow;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        private void TryRePaint(Position position, DataGridViewRow nRow)
        {
            if (nRow.Cells[1].Value == null
                || nRow.Cells[1].Value.ToString() != position.TimeCreate.ToString(_currentCulture))// == false) //AVP убрал, потому что  во вкладке все позиции, дату позиции не обновляло
            {
                nRow.Cells[1].Value = position.TimeCreate.ToString(_currentCulture);
            }
            if (position.TimeClose != position.TimeOpen)
            {
                if (nRow.Cells[2].Value == null
    || nRow.Cells[2].Value.ToString() != position.TimeClose.ToString(_currentCulture))// == false) //AVP убрал потому что во вкладке все позиции, дату позиции не обновляло
                {
                    nRow.Cells[2].Value = position.TimeClose.ToString(_currentCulture);
                }
            }

            if (nRow.Cells[6].Value == null
                || nRow.Cells[6].Value.ToString() != position.State.ToString())
            {
                nRow.Cells[6].Value = position.State;
            }

            if (nRow.Cells[7].Value == null
                || nRow.Cells[7].Value.ToString() != position.MaxVolume.ToStringWithNoEndZero())
            {
                nRow.Cells[7].Value = position.MaxVolume.ToStringWithNoEndZero();
            }

            if (nRow.Cells[8].Value == null
                || nRow.Cells[8].Value.ToString() != position.OpenVolume.ToStringWithNoEndZero())
            {
                nRow.Cells[8].Value = position.OpenVolume.ToStringWithNoEndZero();
            }

            if (nRow.Cells[9].Value == null
                || nRow.Cells[9].Value.ToString() != position.WaitVolume.ToStringWithNoEndZero())
            {
                nRow.Cells[9].Value = position.WaitVolume.ToStringWithNoEndZero();
            }

            int decimalsPrice = position.PriceStep.ToStringWithNoEndZero().DecimalsCount();

            decimalsPrice++;

            decimal openPrice = Math.Round(position.EntryPrice, decimalsPrice);

            if (openPrice == 0)
            {
                if (position.OpenOrders != null &&
                    position.OpenOrders.Count != 0 &&
                    position.State != PositionStateType.OpeningFail)
                {
                    openPrice = position.OpenOrders[position.OpenOrders.Count - 1].Price;
                }
            }

            if (nRow.Cells[10].Value == null
                || nRow.Cells[10].Value.ToString() != openPrice.ToStringWithNoEndZero())
            {
                nRow.Cells[10].Value = openPrice.ToStringWithNoEndZero();
            }

            decimal closePrice = Math.Round(position.ClosePrice, decimalsPrice);

            if (closePrice == 0)
            {
                if (position.CloseOrders != null &&
                    position.CloseOrders.Count != 0 &&
                    position.State != PositionStateType.ClosingFail)
                {
                    closePrice = position.CloseOrders[position.CloseOrders.Count - 1].Price;
                }
            }

            if (nRow.Cells[11].Value == null
                || nRow.Cells[11].Value.ToString() != closePrice.ToStringWithNoEndZero())
            {
                nRow.Cells[11].Value = closePrice.ToStringWithNoEndZero();
            }

            decimal profit = Math.Round(position.ProfitPortfolioPunkt, decimalsPrice);

            if (nRow.Cells[12].Value == null
                || nRow.Cells[12].Value.ToString() != profit.ToStringWithNoEndZero())
            {
                nRow.Cells[12].Value = profit.ToStringWithNoEndZero();
            }

            decimal stopRedLine = Math.Round(position.StopOrderRedLine, decimalsPrice);

            if (nRow.Cells[13].Value == null ||
                nRow.Cells[13].Value.ToString() != stopRedLine.ToStringWithNoEndZero())
            {
                nRow.Cells[13].Value = stopRedLine.ToStringWithNoEndZero();
            }

            decimal stopPrice = Math.Round(position.StopOrderPrice, decimalsPrice);

            if (nRow.Cells[14].Value == null
                || nRow.Cells[14].Value.ToString() != stopPrice.ToStringWithNoEndZero())
            {
                nRow.Cells[14].Value = stopPrice.ToStringWithNoEndZero();
            }

            decimal profitRedLine = Math.Round(position.ProfitOrderRedLine,decimalsPrice);

            if (nRow.Cells[15].Value == null ||
                 nRow.Cells[15].Value.ToString() != profitRedLine.ToStringWithNoEndZero())
            {
                nRow.Cells[15].Value = profitRedLine.ToStringWithNoEndZero();
            }

            decimal profitPrice = Math.Round(position.ProfitOrderPrice,decimalsPrice);

            if (nRow.Cells[16].Value == null ||
                nRow.Cells[16].Value.ToString() != profitPrice.ToStringWithNoEndZero())
            {
                nRow.Cells[16].Value = profitPrice.ToStringWithNoEndZero();
            }

            if (string.IsNullOrEmpty(position.SignalTypeOpen) == false)
            {
                if (nRow.Cells[17].Value == null
                ||
                nRow.Cells[17].Value.ToString() != position.SignalTypeOpen.ToString())
                {
                    nRow.Cells[17].Value = position.SignalTypeOpen;
                }
            }
            if (string.IsNullOrEmpty(position.SignalTypeClose) == false)
            {
                if (nRow.Cells[18].Value == null ||
                nRow.Cells[18].Value.ToString() != position.SignalTypeClose)
                {
                    nRow.Cells[18].Value = position.SignalTypeClose;
                }
            }
        }

        private async void WatcherThreadWorkArea()
        {
            if(_startProgram != StartProgram.IsTester &&
                _startProgram != StartProgram.IsOsTrader)
            {
                return;
            }

            while (true)
            {
                try
                {
                    await Task.Delay(5000);

                    if (MainWindow.ProccesIsWorked == false)
                    {
                        return;
                    }

                    List<Position> openPositions = new List<Position>();
                    List<Position> closePositions = new List<Position>();

                    for (int i = 0; _journals != null && i < _journals.Count; i++)
                    {
                        if (_journals[i].OpenPositions != null
                            && _journals[i].OpenPositions.Count != 0)
                        {
                            openPositions.AddRange(_journals[i].OpenPositions);
                        }
                        if (_journals[i].CloseAllPositions != null)
                        {
                            for (int i2 = _journals[i].CloseAllPositions.Count - 1; i2 > -1 && i2 > _journals[i].CloseAllPositions.Count - 30; i2--)
                            {
                                closePositions.Add(_journals[i].CloseAllPositions[i2]);
                            }
                        }
                    }

                    for (int i = 0; i < closePositions.Count; i++)
                    {
                        for (int i2 = 1; i2 < closePositions.Count; i2++)
                        {// УЛЬТИМАТ. Сортировка пузыриком!
                            if (closePositions[i2].Number < closePositions[i2 - 1].Number)
                            {
                                Position pos = closePositions[i2];
                                closePositions[i2] = closePositions[i2 - 1];
                                closePositions[i2 - 1] = pos;
                            }
                        }
                    }

                    if(openPositions.Count > 100)
                    {
                        openPositions = openPositions.GetRange(openPositions.Count - 100,100);
                    }

                    if (closePositions.Count > 100)
                    {
                        closePositions = closePositions.GetRange(closePositions.Count - 100, 100);
                    }

                    if (_gridOpenPoses != null)
                    {
                        CheckPosition(_gridOpenPoses, openPositions);
                        Sort(_gridOpenPoses);
                    }

                    if (_gridClosePoses != null)
                    {
                        CheckPosition(_gridClosePoses, closePositions);
                        Sort(_gridClosePoses);
                    }

                    
                }
                catch(Exception e)
                {
                    SendNewLogMessage(e.ToString(),LogMessageType.Error);
                    await Task.Delay(5000);
                }
            }
        }

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptions]
        private void Sort(DataGridView grid)
        {
            try
            {
                if(grid == null)
                {
                    return;
                }

                if (grid.InvokeRequired)
                {
                    grid.Invoke(new Action<DataGridView>(Sort), grid);
                    return;
                }

                bool needToSort = false;

                for (int i = 1; i < grid.Rows.Count; i++)
                {
                    if (grid.Rows[i].Cells[0].Value == null
                        || grid.Rows[i - 1].Cells[0].Value == null)
                    {
                        continue;
                    }

                    int numCur = Convert.ToInt32(grid.Rows[i].Cells[0].Value.ToString());
                    int numPrev = Convert.ToInt32(grid.Rows[i - 1].Cells[0].Value.ToString());

                    if (numCur > numPrev)
                    {
                        needToSort = true;
                        break;
                    }
                }

                if (needToSort == false)
                {
                    return;
                }

                List<DataGridViewRow> rows = new List<DataGridViewRow>();

                rows.Add(grid.Rows[0]);

                for (int i = 1; i < grid.Rows.Count; i++)
                {
                    DataGridViewRow curRow = grid.Rows[i];

                    int numCur = Convert.ToInt32(grid.Rows[i].Cells[0].Value.ToString());

                    bool isInArray = false;

                    for (int i2 = 0; i2 < rows.Count; i2++)
                    {
                        int numCurInRowsGrid = Convert.ToInt32(rows[i2].Cells[0].Value.ToString());

                        if (numCur > numCurInRowsGrid)
                        {
                            rows.Insert(i2, curRow);
                            isInArray = true;
                            break;
                        }
                    }

                    if (isInArray == false)
                    {
                        rows.Add(curRow);
                    }
                }

                grid.Rows.Clear();
                grid.Rows.AddRange(rows.ToArray());
            }
            catch(Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// check the position on the correctness of drawing
        /// проверить позиции на правильность прорисовки
        /// </summary>
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptions]
        private void CheckPosition(DataGridView grid, List<Position> positions)
        {
            if (grid.InvokeRequired)
            {
                grid.Invoke(new Action<DataGridView, List<Position>>(CheckPosition),grid, positions);
                return;
            }
            try
            {
                for (int i1 = 0; i1 < positions.Count; i1++)
                {
                    Position position = positions[i1];
                    bool isIn = false;
                    for (int i = 0; i < grid.Rows.Count; i++)
                    {
                        if (grid.Rows[i].Cells[0].Value != null &&
                            grid.Rows[i].Cells[0].Value.ToString() == position.Number.ToString())
                        {
                            TryRePaint(position, grid.Rows[i]);
                            isIn = true;
                            break;
                        }
                    }
                    
                    if (isIn == false)
                    {
                        DataGridViewRow row = GetRow(position);

                        if(row != null)
                        {
                            grid.Rows.Insert(0,row);
                        }
                    }
                }

                for (int i = 0; i < grid.Rows.Count; i++)
                {
                    if (positions.Find(pos => pos.Number == (int)grid.Rows[i].Cells[0].Value) == null)
                    {
                        grid.Rows.Remove(grid.Rows[i]);
                    }
                }

            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #region Исторические позиции

        private void _gridClosePoses_DoubleClick(object sender, EventArgs e)
        {
            try
            {
                PaintPos(_gridClosePoses);
            }
            catch
            {
                // ignore
            }
        }

        private void _gridClosePoses_Click(object sender, EventArgs e)
        {
            try
            {
                MouseEventArgs mouse = (MouseEventArgs)e;

                if (mouse.Button != MouseButtons.Right)
                {
                    return;
                }

                MenuItem[] items = new MenuItem[1];

                items[0] = new MenuItem { Text = OsLocalization.Journal.PositionMenuItem7 };
                items[0].Click += ClosePositionClearDelete_Click;

                ContextMenu menu = new ContextMenu(items);

                _gridClosePoses.ContextMenu = menu;
                _gridClosePoses.ContextMenu.Show(_gridClosePoses, new Point(mouse.X, mouse.Y));
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        void ClosePositionClearDelete_Click(object sender, EventArgs e)
        {
            try
            {
                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Journal.Message3);
                ui.ShowDialog();

                if (ui.UserAcceptActioin == false)
                {
                    return;
                }

                int number;
                try
                {
                    if (_gridClosePoses.CurrentCell == null)
                    {
                        return;
                    }
                    number = Convert.ToInt32(_gridClosePoses.Rows[_gridClosePoses.CurrentCell.RowIndex].Cells[0].Value);
                }
                catch (Exception)
                {
                    return;
                }

                if (UserSelectActionEvent != null)
                {
                    UserSelectActionEvent(GetClosePositionForNumber(number), SignalType.DeletePos);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Активные позиции

        private void _gridOpenPoses_DoubleClick(object sender, EventArgs e)
        {
            try
            {
                PaintPos(_gridOpenPoses);
            }
            catch
            {
                // ignore
            }
        }

        private void _gridAllPositions_Click(object sender, EventArgs e)
        {
            try
            {
                MouseEventArgs mouse = (MouseEventArgs)e;

                if (mouse.Button != MouseButtons.Right)
                {
                    return;
                }

                MenuItem[] items = new MenuItem[5];

                items[0] = new MenuItem { Text = OsLocalization.Journal.PositionMenuItem1 };
                items[0].Click += PositionCloseAll_Click;

                items[1] = new MenuItem { Text = OsLocalization.Journal.PositionMenuItem3 };
                items[1].Click += PositionCloseForNumber_Click;

                items[2] = new MenuItem { Text = OsLocalization.Journal.PositionMenuItem5 };
                items[2].Click += PositionNewStop_Click;

                items[3] = new MenuItem { Text = OsLocalization.Journal.PositionMenuItem6 };
                items[3].Click += PositionNewProfit_Click;

                items[4] = new MenuItem { Text = OsLocalization.Journal.PositionMenuItem7 };
                items[4].Click += PositionClearDelete_Click;

                ContextMenu menu = new ContextMenu(items);

                _gridOpenPoses.ContextMenu = menu;
                _gridOpenPoses.ContextMenu.Show(_gridOpenPoses, new Point(mouse.X, mouse.Y));
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// the user has ordered the closing of all positions
        /// пользователь заказал закрытие всех позиций
        /// </summary>
        void PositionCloseAll_Click(object sender, EventArgs e)
        {
            try
            {
                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Journal.Message5);
                ui.ShowDialog();
                
                if(ui.UserAcceptActioin == false)
                {
                    return;
                }

                if (UserSelectActionEvent != null)
                {
                    UserSelectActionEvent(null, SignalType.CloseAll);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// the user has ordered the closing of the transaction by number
        /// пользователь заказал закрытие сделки по номеру
        /// </summary>
        void PositionCloseForNumber_Click(object sender, EventArgs e)
        {
            try
            {
                int number;
                try
                {
                    if(_gridOpenPoses.CurrentCell == null)
                    {
                        return;
                    }
                    number = Convert.ToInt32(_gridOpenPoses.Rows[_gridOpenPoses.CurrentCell.RowIndex].Cells[0].Value);
                }
                catch (Exception)
                {
                    return;
                }


                if (UserSelectActionEvent != null)
                {
                    UserSelectActionEvent(GetPositionForNumber(number), SignalType.CloseOne);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// the user has ordered a new stop for the position
        /// пользователь заказал новый стоп для позиции
        /// </summary>
        void PositionNewStop_Click(object sender, EventArgs e)
        {
            try
            {
                int number;
                try
                {
                    if(_gridOpenPoses.CurrentCell == null)
                    {
                        return;
                    }
                    number = Convert.ToInt32(_gridOpenPoses.Rows[_gridOpenPoses.CurrentCell.RowIndex].Cells[0].Value);
                }
                catch (Exception)
                {
                    return;
                }

                if (UserSelectActionEvent != null)
                {
                    UserSelectActionEvent(GetPositionForNumber(number), SignalType.ReloadStop);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// the user has ordered a new profit for the position
        /// пользователь заказал новый профит для позиции
        /// </summary>
        void PositionNewProfit_Click(object sender, EventArgs e)
        {
            try
            {
                int number;
                try
                {
                    if(_gridOpenPoses.CurrentCell == null)
                    {
                        return;
                    }
                    number = Convert.ToInt32(_gridOpenPoses.Rows[_gridOpenPoses.CurrentCell.RowIndex].Cells[0].Value);
                }
                catch (Exception)
                {
                    return;
                }

                if (UserSelectActionEvent != null)
                {
                    UserSelectActionEvent(GetPositionForNumber(number), SignalType.ReloadProfit);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// the user has ordered the deletion of a position
        /// пользователь заказал удаление позиции
        /// </summary>
        void PositionClearDelete_Click(object sender, EventArgs e)
        {
            try
            {
                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Journal.Message3);
                ui.ShowDialog();

                if (ui.UserAcceptActioin == false)
                {
                    return;
                }

                int number;
                try
                {
                    if(_gridOpenPoses.CurrentCell == null)
                    {
                        return;
                    }
                    number = Convert.ToInt32(_gridOpenPoses.Rows[_gridOpenPoses.CurrentCell.RowIndex].Cells[0].Value);
                }
                catch (Exception)
                {
                    return;
                }

                if (UserSelectActionEvent != null)
                {
                    UserSelectActionEvent(GetPositionForNumber(number), SignalType.DeletePos);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void PaintPos(DataGridView grid)
        {
            string botTabName;
            int numberRow;

            try
            {
                if (grid.CurrentCell == null)
                {
                    return;
                }

                numberRow = grid.CurrentCell.RowIndex;

                botTabName = grid.Rows[grid.CurrentCell.RowIndex].Cells[3].Value.ToString();
            }
            catch (Exception)
            {
                return;
            }

            if(UserClickOnPositionShowBotInTableEvent != null)
            {
                UserClickOnPositionShowBotInTableEvent(botTabName);
            }

            _rowToPaintInOpenPoses = numberRow;
            _lastClickGrid = grid;

            Task.Run(PaintPos);
        }

        DataGridView _lastClickGrid;

        int _rowToPaintInOpenPoses;

        private async void PaintPos()
        {
            await Task.Delay(200);
            ColoredRow(Color.LightSlateGray);
            await Task.Delay(600);
            ColoredRow(Color.FromArgb(17, 18, 23));
        }

        private void ColoredRow(Color color)
        {
            try
            {
                if (_lastClickGrid.InvokeRequired)
                {
                    _lastClickGrid.Invoke(new Action<Color>(ColoredRow), color);
                    return;
                }

                _lastClickGrid.Rows[_rowToPaintInOpenPoses].DefaultCellStyle.SelectionBackColor = color;
            }
            catch
            {
                return;
            }
        }

        #endregion

        public event Action<string> UserClickOnPositionShowBotInTableEvent;

        public event Action<Position, SignalType> UserSelectActionEvent;

        // messages in log / сообщения в лог 

        /// <summary>
        /// send a new message to the top
        /// выслать новое сообщение на верх
        /// </summary>
        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                System.Windows.MessageBox.Show(message);
            }
        }

        /// <summary>
        /// outgoing message for log
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

    }
}