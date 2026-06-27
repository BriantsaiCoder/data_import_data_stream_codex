using System.Data;
using DCT_data_import;
using Xunit;

namespace DCT_data_import.Tests
{
    public class CalculateSpcTests
    {
        [Fact]
        public void AverageOfSumSquare_WhenAllValuesPass_ComputesCountAverageAndAverageSquare()
        {
            var content = CreateRawDataContent("[1,2,3]", failCount: "0");

            var result = new CalculateSPC().AverageOfSumSquare(content);

            Assert.Single(result);
            Assert.Equal(3m, result[0].pass_n);
            Assert.Equal(2m, result[0].avg);
            Assert.Equal(14m / 3m, result[0].avg2);
        }

        [Fact]
        public void AverageOfSumSquare_WhenFailCountPresent_RemovesValuesOutsideSpec()
        {
            var content = CreateRawDataContent("[1,2,99,-5,3]", failCount: "2", specMax: "10", specMin: "0");

            var result = new CalculateSPC().AverageOfSumSquare(content);

            Assert.Single(result);
            Assert.Equal(3m, result[0].pass_n);
            Assert.Equal(2m, result[0].avg);
            Assert.Equal(14m / 3m, result[0].avg2);
        }

        [Fact]
        public void AverageOfSumSquare_WhenNoValuesCanBeParsed_ReturnsZeroStatistic()
        {
            var content = CreateRawDataContent("[abc,,NaN]", failCount: "0");

            var result = new CalculateSPC().AverageOfSumSquare(content);

            Assert.Single(result);
            Assert.Equal(0m, result[0].pass_n);
            Assert.Equal(0m, result[0].avg);
            Assert.Equal(0m, result[0].avg2);
        }

        private static RawDataContentFormat CreateRawDataContent(
            string values,
            string failCount,
            string specMax = "",
            string specMin = "")
        {
            var content = new RawDataContentFormat();
            var table = new DataTable();
            table.Columns.Add("value");
            table.Columns.Add("# of FAIL");
            table.Columns.Add("Spec MAX");
            table.Columns.Add("Spec MIN");
            table.Rows.Add(values, failCount, specMax, specMin);
            content.LotStatistic.Tables.Add(table);
            return content;
        }
    }
}
