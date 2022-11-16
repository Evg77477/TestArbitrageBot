using OkonkwoOandaV20.TradeLibrary.DataTypes.Position;
using OsEngine.Charts.CandleChart.Elements;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Market.Servers.GateIo.Futures.Response;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using Position = OsEngine.Entity.Position;

namespace OsEngine.Robots.RSI_Bot
{
    public class Rsi_Bot : BotPanel
    {
        public Rsi_Bot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            TabCreate(BotTabType.Simple);
            _tab1 = TabsSimple[1];


            TabCreate(BotTabType.Simple);
            _tab2 = TabsSimple[2];

            _rsi = IndicatorsFactory.CreateIndicatorByName("RSI", name + "RSI", false);
            _rsi = (Aindicator)_tab.CreateCandleIndicator(_rsi, "RsiArea");

            Upline = new LineHorisontal("upline", "RsiArea", false)
            {
                Color = Color.Green,
                Value = 0,
            };
            _tab.SetChartElement(Upline);

            Downline = new LineHorisontal("downline", "RsiArea", false)
            {
                Color = Color.Yellow,
                Value = 0
            };
            _tab.SetChartElement(Downline);

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            Volume = CreateParameter("Volume", 1, 1, 50, 1);
            RsiLength = CreateParameter("Rsi Length", 14, 10, 40, 2);
            UpLineValue = CreateParameter("Up Line Value", 65, 60.0m, 90, 0.5m);
            DownLineValue = CreateParameter("Down Line Value", 35, 10.0m, 40, 0.5m);

            _rsi.ParametersDigit[0].Value = RsiLength.ValueInt;

            _rsi.Save();

            Upline.Value = UpLineValue.ValueDecimal;
            Downline.Value = DownLineValue.ValueDecimal;

            Upline.TimeEnd = DateTime.Now;
            Downline.TimeEnd = DateTime.Now;

            ParametrsChangeByUser += Rsi_Bot_ParametrsChangeByUser;
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
            _tab.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent;

        }

        private BotTabSimple _tab1;
        private BotTabSimple _tab2;
        private BotTabSimple _tab;
        private Aindicator _rsi;
        public LineHorisontal Upline;
        public LineHorisontal Downline;

        public StrategyParameterString Regime;
        public StrategyParameterInt Volume;
        public StrategyParameterInt RsiLength;
        public StrategyParameterDecimal UpLineValue;
        public StrategyParameterDecimal DownLineValue;

        private decimal _rsiNow;
       
        private decimal _firstRsi;
       
        private decimal _secondRsi;
        
        private decimal _thirdRsi;
        
        private decimal _priceRsiNow;
       
        private decimal _priceRsiLast;
       
        private decimal _pointRsiUpNow;
        private decimal _pointRsiDownNow;
        private decimal _pointRsiUpLast;
        private decimal _pointRsiDownLast;


        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (Regime.ValueString == "Off")
            {
                return;
            }

            if (_rsi.DataSeries[0].Values == null)
            {
                return;
            }

            if (_rsi.DataSeries[0].Values.Count < _rsi.ParametersDigit[0].Value + 5)
            {
                return;
            }

            List<Position> positions = _tab.PositionsOpenAll;

            _rsiNow = _rsi.DataSeries[0].Values[_rsi.DataSeries[0].Values.Count - 1];
            _firstRsi = _rsi.DataSeries[0].Values[_rsi.DataSeries[0].Values.Count - 2];
            _secondRsi = _rsi.DataSeries[0].Values[_rsi.DataSeries[0].Values.Count - 3];
            _thirdRsi = _rsi.DataSeries[0].Values[_rsi.DataSeries[0].Values.Count - 4];

            _pointRsiUpLast = _pointRsiUpNow;
            _pointRsiDownLast = _pointRsiDownNow;
            _priceRsiLast = _priceRsiNow;

            if (_firstRsi > _secondRsi && _secondRsi < _thirdRsi && _secondRsi < 45)
            {
                _pointRsiDownNow = _secondRsi;
                _priceRsiNow = candles[candles.Count - 3].Close;
            }
            else if(_firstRsi < _secondRsi && _secondRsi > _thirdRsi && _secondRsi > 55)
            {
                _pointRsiUpNow = _secondRsi;
                _priceRsiNow = candles[candles.Count - 3].Close;
            }

            
            if (_pointRsiDownNow > _pointRsiDownLast && _priceRsiNow < _priceRsiLast && _rsiNow > _firstRsi)
            {
                _tab.BuyAtMarket(Volume.ValueInt);
            }

            if (_pointRsiUpNow < _pointRsiUpLast && _priceRsiNow > _priceRsiLast && _rsiNow < _firstRsi)
            {
                _tab.SellAtMarket(Volume.ValueInt);
            }

            //if (positions.Count > 0)
            //{
            //    Candle candle = candles[candles.Count - 1];

            //    foreach (Position pos in positions)
            //    {
            //        if (pos.State == PositionStateType.Open)
            //        {
            //            if (pos.Direction == Side.Buy)
            //            {
            //                if (candle.Close > pos.EntryPrice && candle.Close - pos.EntryPrice >= pos.EntryPrice - pos.StopOrderPrice)
            //                {
            //                    pos.StopOrderIsActiv = false;

            //                    _tab.CloseAtStop(pos, pos.EntryPrice, pos.EntryPrice - 90 * _tab.Securiti.PriceStep);
            //                }
            //            }
            //            else if (pos.Direction == Side.Sell)
            //            {
            //                if (candle.Close < pos.EntryPrice && candle.Close - pos.EntryPrice <= pos.EntryPrice - pos.StopOrderPrice)
            //                {
            //                    pos.StopOrderIsActiv = false;

            //                    _tab.CloseAtStop(pos, pos.EntryPrice, pos.EntryPrice + 90 * _tab.Securiti.PriceStep);
            //                }
            //            }
            //        }
            //    }
            //}

            Upline.Refresh();
            Downline.Refresh();
        }


        private void _tab_PositionOpeningSuccesEvent(Position pos)
        {
            if (pos.Direction == Side.Buy)
            {

                _tab.CloseAtStop(pos, pos.EntryPrice - 100, pos.EntryPrice - 120);

                decimal _takeProfit = pos.EntryPrice + 250;

                _tab.CloseAtProfit(pos, _takeProfit, _takeProfit);
            }
            else if (pos.Direction == Side.Sell)
            {

                _tab.CloseAtStop(pos, pos.EntryPrice + 100, pos.EntryPrice + 120);

                decimal _takeProfit = pos.EntryPrice - 250;

                _tab.CloseAtProfit(pos, _takeProfit, _takeProfit);
            }

        }



        private void Rsi_Bot_ParametrsChangeByUser()
        {
            if (_rsi.ParametersDigit[0].Value != RsiLength.ValueInt)
            {
                _rsi.ParametersDigit[0].Value = RsiLength.ValueInt;
                _rsi.Reload();
            }

            Upline.Value = UpLineValue.ValueDecimal;
            Upline.Refresh();
            Downline.Value = DownLineValue.ValueDecimal;
            Downline.Refresh();
        }

        public override string GetNameStrategyType()
        {
            return "Rsi_Bot";
        }

        public override void ShowIndividualSettingsDialog()
        {
            throw new NotImplementedException();
        }
    }
}
