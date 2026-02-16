namespace Skojjt.Core.Entities;

public class Semester
{
    public int Id { get; set; }
    public int Year { get; set; }
    public bool IsAutumn { get; set; }
    public static int GenerateId(int year, bool isAutumn) => (year * 10) + (isAutumn ? 1 : 0);
	public static int GenerateId(int year, Season season) => (year * 10) + (int)season;
	public string DisplayName => string.Format("{0} {1}", IsAutumn ? "HT" : "VT", Year);

	public enum Season { VT=0, HT=1 };


	public bool IsValidDate(DateOnly date)
	{
		if (IsAutumn)
		{
			// Autumn semester: July 1 to December 31
			return date >= new DateOnly(Year, 7, 1) && date <= new DateOnly(Year, 12, 31);
		}
		else
		{
			// Spring semester: January 1 to June 30
			return date >= new DateOnly(Year, 1, 1) && date <= new DateOnly(Year, 6, 30);
		}
	}
}