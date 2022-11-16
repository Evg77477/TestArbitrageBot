
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;



namespace OsEngine.Robots.ArbitrageBot
{
    /* 
     * Логика работы бота.
     * 
     * 1. До начала работы выбрать пару инструментов, на которой предполагается торговля.
     * 
     * 2. Определить спред, обычный для данной пары (базовый спред).
     * Спред может определяться, как разница цены инструментов (Price.инстр 1 - Price.инстр 2), так и в процентах по формуле:
     * (цена фьючерса - спотовая цена) / спотовая цена.
     * 
     * 3. В настройках вкладки 1, в зависимости от выбранного типа расчета спреда, задать формулу расчета спреда: А0 - А1 
     *  или:  ( А(фьючерс) - А(спот) ) / А(спот)
     *  
     * 4. В настройках торговли бота задать по модулю в процентах или в абсолютных величинах, 
     * в зависимости от выбранного в п. 2 и п. 3 способа расчета спреда:
     *  - базовый спред; 
     *  - спред, при котором бот будет открывать позиции; 
     *  - изменение спреда в процентах, после которого бот откроет дополнительные позиции.
     *  
     *  5. При достижении спредом заданного показателя бот будет открывать позиции. 
     *  После возвращения спреда к базовому уровню позиции будут закрыты.
     *  При продолжающемся увеличении спреда, в случае если увеличение спреда, расчитанное в процентах, достигнет заданной величины, 
     *  бот откроет дополнительные позиции, которые также будут закрыты при возвращении спреда к базовому уровню.
     */

    public class TestArbitrageBot : BotPanel
    {
        public TestArbitrageBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Index); 
            _tabIndex = TabsIndex[0];          

            TabCreate(BotTabType.Simple);
            _tabFirst = TabsSimple[0];

            TabCreate(BotTabType.Simple);
            _tabSecond = TabsSimple[1];

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyClosePosition" });

            Volume = CreateParameter("Volume", 0.1m, 0.1m, 50m, 0.01m);

            BaseSpread = CreateParameter("BaseSpread", 1m, 0.1m, 50m, 0.1m);

            SpreadForTrade = CreateParameter("SpreadForTrade", 1m, 0.1m, 50m, 0.1m);

            ChangeSpreadForNewOpening = CreateParameter("ChangeSpreadForNewOpening", 10, 10, 100, 5);

            Slippage = CreateParameter("Slipage", 0, 0, 20, 1);

            ParametrsChangeByUser += TestArbitrageBot_ParametrsChangeByUser;

            _tabIndex.SpreadChangeEvent += _tabIndex_SpreadChangeEvent;

        }    


       #region Fields ==================================================================

        /// <summary>
        /// Вкладка для первого инструмента
        /// </summary>
        private BotTabSimple _tabFirst;


        /// <summary>
        /// Вкладка для второго инструмента
        /// </summary>
        private BotTabSimple _tabSecond;


        /// <summary>
        /// regime
        /// режим работы робота
        /// </summary>
        public StrategyParameterString Regime;

        /// <summary>
        /// volume
        /// Объём одной сделки
        /// </summary>
        public StrategyParameterDecimal Volume;

        /// <summary>
        /// volume
        /// Обычное значение спреда для данной пары
        /// </summary>
        public StrategyParameterDecimal BaseSpread;

        /// <summary>
        /// volume
        /// Спред, при котором робот осуществляет вход в сделку
        /// </summary>
        public StrategyParameterDecimal SpreadForTrade;

        /// <summary>
        /// volume
        /// Процент увеличения спреда, при котором открывается дополнительная позиция
        /// </summary>
        public StrategyParameterInt ChangeSpreadForNewOpening;

        /// <summary>
        /// slippage / проскальзывание
        /// </summary>
        public StrategyParameterInt Slippage;

        /// <summary>
        /// Вкладка для расчета спреда
        /// </summary>
        private BotTabIndex _tabIndex;


        /// <summary>
        /// Спред, при котором открылась позиция
        /// </summary>
        private decimal _spread;

        #endregion =====================================

        #region Methods =============================================================

        private void TestArbitrageBot_ParametrsChangeByUser()
        {            
            if (_tabFirst.TimeFrame != _tabSecond.TimeFrame)
            {
                MessageBox.Show("Таймфремы инструментов не совпадают, измените настройки бота!");
            }
            
            if (SpreadForTrade.ValueDecimal == 0)
            {
                MessageBox.Show("Задайте спред для открытия позиции");
            }
        }

        private void _tabIndex_SpreadChangeEvent(List<Candle> candles)
        {
            decimal spread = Math.Abs(candles.Last().Close);

            if (Regime.ValueString == "Off"
                || _tabFirst.CandlesFinishedOnly == null
                || _tabSecond.CandlesFinishedOnly == null
                || candles == null
                || candles.Count < 10
                || _tabFirst.CandlesFinishedOnly.Count < 10
                || _tabSecond.CandlesFinishedOnly.Count < 10) return; 

            
            if (Regime.ValueString == "On"
                && candles.Count > 10
                && spread >= SpreadForTrade.ValueDecimal               
                && _tabFirst.PositionsOpenAll.Count == 0
                && _tabSecond.PositionsOpenAll.Count == 0)
            {
                _spread = spread;
                TradeLogicOpen();
            }
            else if((Regime.ValueString == "On"
                || Regime.ValueString == "OnlyClosePosition")
                && (_tabFirst.PositionsOpenAll != null
                || _tabSecond.PositionsOpenAll != null)
                && (_tabFirst.PositionsOpenAll.Count > 0
                || _tabSecond.PositionsOpenAll.Count > 0))
            {
                TradeLogic(spread);
            }            
        }


        private void TradeLogicOpen()
        {
            List<Candle> candlesFirst = _tabFirst.CandlesFinishedOnly;
            List<Candle> candlesSecond = _tabSecond.CandlesFinishedOnly;

            decimal priceFirst = candlesFirst.Last().Close;
            decimal priceSecond = candlesSecond.Last().Close;

            if (priceFirst > priceSecond)
            {
                _tabFirst.SellAtLimit(Volume.ValueDecimal, _tabFirst.PriceBestBid - Slippage.ValueInt * _tabFirst.Securiti.PriceStep);
                _tabSecond.BuyAtLimit(Volume.ValueDecimal, _tabSecond.PriceBestAsk + Slippage.ValueInt * _tabSecond.Securiti.PriceStep);
            }
            else if (priceFirst < priceSecond)
            {
                _tabFirst.BuyAtLimit(Volume.ValueDecimal, _tabFirst.PriceBestAsk + Slippage.ValueInt * _tabFirst.Securiti.PriceStep);
                _tabSecond.SellAtLimit(Volume.ValueDecimal, _tabSecond.PriceBestBid - Slippage.ValueInt * _tabSecond.Securiti.PriceStep);
            }

        }


        private void TradeLogic(decimal spread)
        {
            decimal ChangeSpreadInPercent = (spread - _spread) * 100 / _spread;

            if (spread <= BaseSpread.ValueDecimal)
            {
                TradeLogicClose();
            }
            else if (spread > _spread
                && ChangeSpreadInPercent >= ChangeSpreadForNewOpening.ValueInt
                && Regime.ValueString != "OnlyClosePosition")
            {
                _spread = spread;
                TradeLogicOpen();
            }
        }


        private void TradeLogicClose()
        {           
            List<Position> positionsTabFirst = _tabFirst.PositionsOpenAll;
            List<Position> positionsTabSecond = _tabSecond.PositionsOpenAll;

            if (positionsTabFirst != null
                && positionsTabFirst.Count > 0)
            {
                foreach (Position pos in positionsTabFirst)
                {
                    if (pos.State == PositionStateType.Open
                       && pos.Direction == Side.Buy)
                    {
                        _tabFirst.CloseAtLimit(pos, _tabFirst.PriceBestBid - Slippage.ValueInt * _tabFirst.Securiti.PriceStep, pos.OpenVolume);
                    }
                    if (pos.State == PositionStateType.Open
                       && pos.Direction == Side.Sell)
                    {
                        _tabFirst.CloseAtLimit(pos, _tabFirst.PriceBestAsk + Slippage.ValueInt * _tabFirst.Securiti.PriceStep, pos.OpenVolume);
                    }
                }
            }

            if (positionsTabSecond != null
                && positionsTabSecond.Count > 0)
            {
                foreach (Position pos in positionsTabSecond)
                {
                    if (pos.State == PositionStateType.Open
                          && pos.Direction == Side.Buy)
                    {
                        _tabSecond.CloseAtLimit(pos, _tabSecond.PriceBestBid - Slippage.ValueInt * _tabSecond.Securiti.PriceStep, pos.OpenVolume);
                    }
                    if (pos.State == PositionStateType.Open
                       && pos.Direction == Side.Sell)
                    {
                        _tabSecond.CloseAtLimit(pos, _tabSecond.PriceBestAsk + Slippage.ValueInt * _tabSecond.Securiti.PriceStep, pos.OpenVolume);
                    }
                }
            }
               
            
        }

        public override string GetNameStrategyType()
        {
            return "TestArbitrageBot";
        }

        public override void ShowIndividualSettingsDialog()
        {
          
        }

        #endregion ===================================
    }
}
