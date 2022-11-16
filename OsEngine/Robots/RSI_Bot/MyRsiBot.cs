using OsEngine.Charts.CandleChart.Elements;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Market.Servers.Tinkoff.TinkoffJsonSchema;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace OsEngine.Robots.RSI_Bot
{
    public class MyRsiBot : BotPanel
    {
        public MyRsiBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

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
            Volume = CreateParameter("Volume", 1, 1.0m, 50, 4);            
            RsiLength = CreateParameter("Rsi Length", 14, 10, 40, 2);
            UpLineValue = CreateParameter("Up Line Value", 65, 60.0m, 90, 0.5m);
            DownLineValue = CreateParameter("Down Line Value", 35, 10.0m, 40, 0.5m);

            _rsi.ParametersDigit[0].Value = RsiLength.ValueInt;

            _rsi.Save();

            Upline.Value = UpLineValue.ValueDecimal;            
            Downline.Value = DownLineValue.ValueDecimal;

            Upline.TimeEnd = DateTime.Now;
            Downline.TimeEnd = DateTime.Now;

            ParametrsChangeByUser += MyRsiBot_ParametrsChangeByUser;

            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
            _tab.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent;


        }

        private void _tab_PositionOpeningSuccesEvent(Position pos)
        {
           _tab.CloseAtStop(pos, pos.EntryPrice - 100, pos.EntryPrice - 120);
        }

        private BotTabSimple _tab;
        private Aindicator _rsi;
        public LineHorisontal Upline;
        public LineHorisontal Downline;

        public StrategyParameterString Regime;
        public StrategyParameterDecimal Volume;
        public StrategyParameterInt RsiLength;
        public StrategyParameterDecimal UpLineValue;
        public StrategyParameterDecimal DownLineValue;

        private decimal _lastPrice;
        private decimal _controlRsi; //Текущее значение Rsi
        private decimal _firstRsi; //Предыдущее значение Rsi
        private decimal _secondRsi; // Предпоследенне значение Rsi
        private decimal _thirdRsi; // Последнее значение Rsi
        private decimal _pointRsi = 0; // Текущая точка смены направления Rsi
        private decimal _lastpointRsi = 0; // Предыдущая точка смены направления Rsi
        private decimal _priceRsi; // Значение цены при текущем Rsi
        private decimal _lastPriceRsi; // Значение цены при предыдущем Rsi
        Position position;

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

            _lastPrice = candles[candles.Count - 1].Close;
            _firstRsi = _rsi.DataSeries[0].Values[_rsi.DataSeries[0].Values.Count - 2];
            _secondRsi = _rsi.DataSeries[0].Values[_rsi.DataSeries[0].Values.Count - 3];
            _thirdRsi = _rsi.DataSeries[0].Values[_rsi.DataSeries[0].Values.Count - 4];
            _controlRsi = _rsi.DataSeries[0].Values[_rsi.DataSeries[0].Values.Count - 1];


            List<Position> positions = _tab.PositionsOpenAll; // Все позиции          
            
            if (positions != null && positions.Count != 0)
            {
                position = positions[0];
            }

            if (positions.Count > 0)
            {
                //Trailing(positions);
                Candle candle = candles[candles.Count - 1];

                if (candle.Close > position.EntryPrice && candle.Close - position.EntryPrice >= position.EntryPrice - position.StopOrderPrice)
                {
                    position.StopOrderIsActiv = false;

                    _tab.CloseAtStop(position, position.EntryPrice, position.EntryPrice - 80 * _tab.Securiti.PriceStep);
                }

                //return;

                //if (position.EntryPrice > _lastPrice - 100)
                //{
                //    _tab.CloseAtStop(position, _lastPrice - 150, _lastPrice - 200);
                //}
            }

            if (positions != null && positions.Count > 0 && (_lastPrice - position.EntryPrice) >= 250) // Добавить верхний разворот
            {
                decimal _takeProfit = position.EntryPrice + 300;
                
                _tab.CloseAtProfit(position, _takeProfit, _takeProfit);
            }

            if (_firstRsi <= 35)
            {
                
                _lastpointRsi = _pointRsi;
                _lastPriceRsi = _priceRsi;

                if (_firstRsi > _secondRsi && _secondRsi < _thirdRsi)
                {
                    _pointRsi = _secondRsi;
                    _priceRsi = candles[candles.Count - 1].Close;
                }             

                if (_pointRsi > _lastpointRsi && _priceRsi < _lastPriceRsi && _controlRsi > _pointRsi
                    && positions.Count == 0) 
                {
                    _tab.BuyAtMarket(Volume.ValueDecimal);
                }
            }

            Upline.Refresh();
            Downline.Refresh();

        }


        //private void Trailing(List<Position> positions)
        //{
        //    foreach (Position pos in positions)
        //    {
        //        if (pos.State == PositionStateType.Open)
        //        {
        //            if (pos.Direction == Side.Buy)
        //            {
        //                _tab.CloseAtTrailingStop(pos, _lastPrice - 100, _lastPrice - 120 * _tab.Securiti.PriceStep);
        //            }
        //            else if (pos.Direction == Side.Sell)
        //            {
        //                _tab.CloseAtTrailingStop(pos, _lastPrice - 55, _lastPrice + 250 * _tab.Securiti.PriceStep);
        //            }
        //        }
        //    }
        //}


        private void MyRsiBot_ParametrsChangeByUser()
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
            return "MyRsiBot";
        }

       
        
        
        public override void ShowIndividualSettingsDialog()
        {
            throw new NotImplementedException();
        }
    }
}
