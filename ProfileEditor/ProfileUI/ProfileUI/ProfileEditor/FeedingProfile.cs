using ApexCharts;
using Microsoft.AspNetCore.Components;
using Newtonsoft.Json.Linq;
using System.Data.Common;
using System.Xml.Linq;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;

using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ProfileUI.ProfileEditor
{
    public class FeedingProfile
    {
        private readonly string _tableName = "editorfeedingdata";
        public enum ProfileLineType { None, Linear, Polynomial, MovingAverage, Smoothing }
        public ProfileLineType CurrentUsedType = ProfileLineType.None;
        private ProfileCore profileCore;
        public List<CustomeGridRow> Data = new();

        public decimal FeedingProfileOffset = 0;
        public bool UseOffset = false;

        public bool UseIntercept = false;
        public decimal InterceptValue = 0;

        public int PolynomialValue = 2;
        public int MovingAveragePeriod = 2;
        public decimal SmoothingFactor = 0;
        public FeedingProfile(ProfileCore profileCore)
        {
            Data = new List<CustomeGridRow>();
            Data.Add(new CustomeGridRow
            {
                Col1 = -1,
                Col2 = -1,
                Col3 = -1,
                IsNewLine = true,
                IsSelected = true,
                RowId = Guid.NewGuid().ToString()
            });
            this.profileCore = profileCore;
            profileCore._db.UpdateData(_tableName, Data.Cast<MainGridRow>().ToList());
        }

        public void ClearData()
        {
            Data.Clear();
        }

        public void RecalculateFeedingProfile()
        {
            if (profileCore.Data.Count < 2 && CurrentUsedType != ProfileLineType.None)
            {
                Data = new List<CustomeGridRow>();
                for (int i = 0; i < profileCore.Data.Count; i++)
                {
                    Data.Add(profileCore.Data[i]);
                }
                profileCore._db.UpdateData(_tableName, Data.Cast<MainGridRow>().ToList());
                return;
            }

            switch (CurrentUsedType)
            {
                case ProfileLineType.None:
                    UseInterpolation();
                    break;
                case ProfileLineType.Linear:
                    UseLinearTrendLine();
                    break;
                case ProfileLineType.Polynomial:
                    UsePolynomialTrendLine();
                    break;
                case ProfileLineType.MovingAverage:
                    UsedMovingAverageTrendLine();
                    break;
                case ProfileLineType.Smoothing:
                    UseSmoothTrendLine();
                    break;
            }
            profileCore._db.UpdateData(_tableName, Data.Cast<MainGridRow>().ToList());
        }
        private void UseSmoothTrendLine()
        {
            if (profileCore.Data.Count < 2)
                return;

            var interArrays = InterpolateValues(profileCore.Data.Select(data => data.Col1).ToList(), profileCore.Data.Select(data => data.Col2).ToList());
            var hoursInt = interArrays.hoursInt;
            var valuesInt = interArrays.valuesInt;
            // Extract data points

            Data = new List<CustomeGridRow>();

            decimal prevTime = hoursInt[0];
            decimal prevFedg = valuesInt[0];
            Data.Add(new CustomeGridRow { Col1 = hoursInt[0], Col2 = (valuesInt[0] >= 0 ? valuesInt[0] : 0), Col3 = 0 });
            decimal smoothFactor = SmoothingFactor / 100;
            for (int i = 1; i < hoursInt.Count; i++)
            {
                decimal hours = hoursInt[i];
                decimal y = valuesInt[i] * (1 - smoothFactor) + prevFedg * smoothFactor;
                decimal speed = 0;

                if (UseOffset)
                    y = y + y * FeedingProfileOffset;

                if (y < 0)
                    y = 0;

                if (i > 0)
                {
                    speed = Math.Round((y - prevFedg) / (hours - prevTime), 3);
                }
                Data.Add(new CustomeGridRow { Col1 = Math.Round(hours, 3), Col2 = Math.Round(y, 3), Col3 = speed });
                prevTime = hours;
                prevFedg = y;

            }
        }
        private void UseInterpolation()
        {
            var interArrays = InterpolateValues(profileCore.Data.Select(data => data.Col1).ToList(), profileCore.Data.Select(data => data.Col2).ToList());
            var hoursInt = interArrays.hoursInt;
            var valuesInt = interArrays.valuesInt;

            decimal prevTime = 0;
            decimal prevFedg = 0;
            Data = new List<CustomeGridRow>();
            for (int i = 0; i < hoursInt.Count; i++)
            {
                decimal hours = hoursInt[i];
                decimal y = valuesInt[i];
                decimal speed = 0;
                if (UseOffset)
                    y = y + y * FeedingProfileOffset / 100;

                if (y < 0)
                    y = 0;

                if (i > 0)
                {
                    speed = Math.Round((y - prevFedg) / (hours - prevTime), 3);
                }
                Data.Add(new CustomeGridRow { Col1 = Math.Round(hours, 3), Col2 = Math.Round(y, 3), Col3 = speed });
                prevTime = hours;
                prevFedg = y;

            }
        }
        private (List<decimal> hoursInt, List<decimal> valuesInt) InterpolateValues(List<decimal> hours, List<decimal> values)
        {
            List<decimal> hoursInt = new List<decimal>();
            List<decimal> valuesInt = new List<decimal>();
            if (hours.Count <= 0 || values.Count <= 0)
                return (hoursInt, valuesInt);

            hoursInt.Add(hours.First());
            valuesInt.Add(values.First());

            for (int i = 1; i < hours.Count; i++)
            {
                decimal start = hoursInt.Last() * 60;
                decimal end = hours[i] * 60;
                var lastY = valuesInt.Last();
                var newY = values[i];
                for (decimal j = start + 1; j <= end; j++)
                {
                    hoursInt.Add(j / 60);
                    decimal valueY = lastY + (j - start) / (end - start) * (newY - lastY);
                    valuesInt.Add(valueY);
                }
            }
            return (hoursInt, valuesInt);
        }

        private void UseLinearTrendLine()
        {
            if (profileCore.Data.Count <= 2)
                return;

            decimal m = 0;
            decimal b = 0;
            if (!UseIntercept)
            {
                int n = profileCore.Data.Count;
                decimal sumX = 0;
                decimal sumY = 0;
                decimal sumXY = 0;
                decimal sumX2 = 0;
                for (int i = 0; i < profileCore.Data.Count; i++)
                {
                    decimal hours = profileCore.Data[i].Col1;
                    decimal mass = profileCore.Data[i].Col2;
                    sumX += hours;
                    sumY += mass;
                    sumXY += mass * hours;
                    sumX2 += hours * hours;
                }
                m = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
                b = (sumY - m * sumX) / n;
            }
            else
            {
                // Calculate slope (m) with fixed intercept
                double numerator = 0;
                double denominator = 0;
                for (int i = 0; i < profileCore.Data.Count; i++)
                {
                    double hours = (double)profileCore.Data[i].Col1;
                    double mass = (double)profileCore.Data[i].Col2;
                    numerator += hours * (mass - (double)InterceptValue);
                    denominator += hours * hours;
                }
                m = (decimal)(numerator / denominator);
                b = InterceptValue;
            }

            var interArrays = InterpolateValues(profileCore.Data.Select(data => data.Col1).ToList(), profileCore.Data.Select(data => data.Col2).ToList());
            var hoursInt = interArrays.hoursInt;
            decimal prevTime = 0;
            decimal prevFedg = 0;
            Data = new List<CustomeGridRow>();
            for (int i = 0; i < hoursInt.Count; i++)
            {
                decimal hours = hoursInt[i];
                decimal y = m * hours + b;
                if (UseOffset)
                    y = y + y * FeedingProfileOffset;

                decimal speed = 0;
                if (y < 0)
                    y = 0;
                if (i > 0)
                {
                    speed = Math.Round((y - prevFedg) / (hours - prevTime), 3);
                }
                Data.Add(new CustomeGridRow { Col1 = Math.Round(hours, 3), Col2 = Math.Round(y, 3), Col3 = speed });
                prevTime = hours;
                prevFedg = y;
            }
        }
        private void UsedMovingAverageTrendLine()
        {
            if (profileCore.Data.Count <= 2)
                return;

            var interArrays = InterpolateValues(profileCore.Data.Select(data => data.Col1).ToList(), profileCore.Data.Select(data => data.Col2).ToList());
            var hoursInt = interArrays.hoursInt;
            var valuesInt = interArrays.valuesInt;
            // Extract data points
            var movingAverages = new List<decimal>();
            int averagingPeriod = MovingAveragePeriod * hoursInt.Count / 100;

            var skipPart = InterpolateValues(new List<decimal> { hoursInt[0], hoursInt[averagingPeriod] }, new List<decimal> { valuesInt[0], valuesInt.Take(averagingPeriod).Average() }).valuesInt;

            for (int i = 0; i < hoursInt.Count; i++)
            {
                if (i < averagingPeriod)
                {
                    movingAverages.Add(skipPart[i]); // Not enough points for a full period
                }
                else
                {
                    movingAverages.Add(valuesInt.Skip(i - averagingPeriod + 1).Take(averagingPeriod).Average());
                }
            }
            decimal prevTime = 0;
            decimal prevFedg = 0;
            Data = new List<CustomeGridRow>();
            for (int i = 0; i < hoursInt.Count; i++)
            {
                if (!decimal.IsNegative(movingAverages[i]))
                {
                    decimal hours = hoursInt[i];
                    decimal y = movingAverages[i];
                    decimal speed = 0;
                    if (UseOffset)
                        y = y + y * FeedingProfileOffset;
                    if (y < 0)
                        y = 0;

                    if (i > 0)
                    {
                        speed = Math.Round((y - prevFedg) / (hours - prevTime), 3);
                    }
                    Data.Add(new CustomeGridRow { Col1 = Math.Round(hours, 3), Col2 = Math.Round(y, 3), Col3 = speed });
                    prevTime = hours;
                    prevFedg = y;

                }
            }
        }
        private void UsePolynomialTrendLine()
        {
            if (profileCore.Data.Count <= PolynomialValue)
            {
                return;
            }

            // Use MathNet.Numerics.LinearAlgebra.Vector explicitly to avoid ambiguity
            double[] coefficients;
            if (!UseIntercept)
            {
                coefficients = Fit.Polynomial(
                    profileCore.Data.Select(data => (double)data.Col1).ToArray(),
                    profileCore.Data.Select(data => (double)data.Col2).ToArray(),
                    PolynomialValue
                );
            }
            else
            {
                var designMatrix = Matrix<double>.Build.Dense(profileCore.Data.Count, PolynomialValue, (i, j) => Math.Pow((double)profileCore.Data[i].Col1, j + 1));
                var yVector = Vector<double>.Build.Dense(profileCore.Data.Select(y => (double)(y.Col2 - InterceptValue)).ToArray());

                coefficients = designMatrix.TransposeThisAndMultiply(designMatrix).LU().Solve(designMatrix.TransposeThisAndMultiply(yVector)).ToArray();
            }
            var interArrays = InterpolateValues(profileCore.Data.Select(data => data.Col1).ToList(), profileCore.Data.Select(data => data.Col2).ToList());
            var hoursInt = interArrays.hoursInt;
            Data = new List<CustomeGridRow>();
            double prevTime = 0;
            double prevFedg = 0;
            double d_InterceptValue = (double)InterceptValue;
            double d_FeedingProfileOffset = (double)FeedingProfileOffset / 100;
            for (int i = 0; i < hoursInt.Count; i++)
            {
                double y = UseIntercept ? d_InterceptValue : 0;
                double offset = UseIntercept ? 1 : 0;
                double hours = (double)hoursInt[i];
                double speed = 0;
                for (int j = 0; j < coefficients.Count(); j++)
                {
                    y += coefficients[j] * Math.Pow(hours, j + offset); 
                }

                if (UseOffset)
                    y = y + y * d_FeedingProfileOffset;

                if (y < 0)
                    y = 0;

                if (i > 0)
                {
                    speed = Math.Round((y - prevFedg) / (hours - prevTime), 3);
                }
                Data.Add(new CustomeGridRow { Col1 = (decimal)Math.Round(hours,3), Col2 = (decimal)Math.Round(y,3), Col3 = (decimal)speed });
                prevTime = hours;
                prevFedg = y;
            }
        }

    }
    
}
