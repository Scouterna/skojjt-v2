namespace Skojjt.Core.Entities;

public class Semester
{
    public Semester(int id)
    {
        Id = id;
        Year = id / 10;
        IsAutumn = (id % 10) == 1;
        if (Year < 2000 || Year > 2100)
        {
            throw new ArgumentException($"Year {Year} is out of valid range (2000-2100)");
        }
    }
    public Semester(int year, bool isAutumn)
    {
        Id = GenerateId(year, isAutumn);
        Year = year;
        IsAutumn = isAutumn;
        if (year < 2000 || year > 2100)
        {
            throw new ArgumentException($"Year {year} is out of valid range (2000-2100)");
        }
    }
    public Semester(int id, int year, bool isAutumn)
    {
        Id = id;
        Year = year;
        IsAutumn = isAutumn;
        if (year < 2000 || year > 2100)
        {
            throw new ArgumentException($"Year {year} is out of valid range (2000-2100)");
        }
        if (GenerateId(year, isAutumn) != id)
        {
            throw new ArgumentException($"Id {id} does not match generated id for year {year} and isAutumn {isAutumn}");
        }
    }
    public static Semester GetCurrentSemester()
    {
        var now = DateTime.Now;
        return new Semester(now.Year, now.Month >= 7);
    }

    public (DateOnly fromDate, DateOnly toDate) GetStartAndEndDates()
    {
        if (IsAutumn)
        {
            return (new DateOnly(Year, 7, 1), new DateOnly(Year, 12, 31));
        }
        else
        {
            return (new DateOnly(Year, 1, 1), new DateOnly(Year, 6, 30));
        }
    }

    public int Id { get; private set; }
    public int Year { get; private set; }
    public bool IsAutumn { get; private set; }
    public static int GenerateId(int year, bool isAutumn) => (year * 10) + (isAutumn ? 1 : 0);
    public static int GenerateId(int year, Season season) => (year * 10) + (int)season;
    public string DisplayName => string.Format("{0} {1}", IsAutumn ? "HT" : "VT", Year);

    public enum Season { VT = 0, HT = 1 };

    public bool IsValidDate(DateOnly date)
    {
        (var startDate, var endDate) = GetStartAndEndDates();
        return date >= startDate && date <= endDate;
    }

    public Semester GetOtherSemesterSameYear()
    {
        return new Semester(Year, !IsAutumn);
    }
}