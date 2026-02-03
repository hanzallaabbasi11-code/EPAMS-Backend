namespace EPAMS.Controllers.Student
{
    internal class ExcelDataSetConfiguration
    {
        public ExcelDataSetConfiguration()
        {
        }

        public System.Func<object, ExcelDataTableConfiguration> ConfigureDataTable { get; set; }
    }
}