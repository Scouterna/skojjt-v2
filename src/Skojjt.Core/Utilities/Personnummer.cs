namespace Skojjt.Core.Utilities;

public class Personnummer : IEquatable<Personnummer>, IComparable<Personnummer>
{
	private readonly string _personnummer;
	public Personnummer(string nummer)
	{
		_personnummer = nummer.Replace("-", "");
	}

	public override string ToString()
	{
		return _personnummer;
	}

	public string ToFormattedString() // Format as YYYYMMDD-NNNN
	{
		if (_personnummer.Length != 12)
		{
			return _personnummer;
		}
		return $"{_personnummer.Substring(0, 8)}-{_personnummer.Substring(8, 4)}";
	}

	public bool CorrectChecksum()
	{
		if (_personnummer.Length != 12) return false;
		var checknumber = CalculateCheckDigit();
		return Int32.Parse(_personnummer.AsSpan(11, 1)) == checknumber;
	}

	public int CalculateCheckDigit()
	{
		int sum = 0;

		for (int i = 2; i < _personnummer.Length - 1; i++)
		{
			int v = (int)Char.GetNumericValue(_personnummer[i]);
			// integer multiply every every second with 2, 1, 2 ..
			v *= 2 - (i & 1);

			if (v > 9)
			{
				// if larger than 9: 10-18, add the separate numbers together
				sum += 1 + (v % 10);
			}
			else
			{
				sum += v;
			}
		}
		return (10 - sum % 10) % 10; // subtract from next higher 10
	}

	public bool IsValid
	{
		get
		{
			if (_personnummer.Length != 12)
			{
				return false;
			}
			if (_personnummer.Any(c => !Char.IsDigit(c)))
			{
				return false;
			}
			return CorrectChecksum();
		}
	}
	public int Year => Int32.Parse(_personnummer.AsSpan(0, 4));
	public int Month => Int32.Parse(_personnummer.AsSpan(4, 2));
	public int Day
	{
		get
		{
			var day = Int32.Parse(_personnummer.AsSpan(6, 2));
			if (day < 60) return day;
			return day - 60;
		}
	}
	public bool IsSamordningsnummer => Int32.Parse(_personnummer.AsSpan(6, 2)) >= 60;
	public bool IsFemale => (Int32.Parse(_personnummer.AsSpan(10, 1)) & 1) == 0;
	public bool IsMale => (Int32.Parse(_personnummer.AsSpan(10, 1)) & 1) != 0;
	public DateOnly BirthDay => new DateOnly(Year, Month, Day);
	public string BirthDayString => BirthDay.ToString("yyyy-MM-dd");

	public bool Equals(Personnummer? other)
	{
		if (ReferenceEquals(null, other)) return false;
		return String.Equals(_personnummer, other._personnummer);
	}

	public int CompareTo(Personnummer? other)
	{
		if (ReferenceEquals(null, other)) return 1;
		return String.Compare(_personnummer, other._personnummer);
	}

	public override bool Equals(object? obj)
	{
		if (ReferenceEquals(this, obj))
		{
			return true;
		}
		if (ReferenceEquals(obj, null))
		{
			return false;
		}
		return Equals((Personnummer?)obj);
	}

	public override int GetHashCode()
	{
		// Century number as first digit in the int
		int hash = (int.Parse(_personnummer.AsSpan(0, 2)) - 18) * 1000000000;
		// behind the centry are all other digits except the check number
		hash += int.Parse(_personnummer.AsSpan(2, 9));
		return hash;
	}

	public static bool operator ==(Personnummer? left, Personnummer? right)
	{
		if (ReferenceEquals(left, null))
		{
			return ReferenceEquals(right, null);
		}

		return left.Equals(right);
	}

	public static bool operator !=(Personnummer? left, Personnummer? right)
	{
		return !(left == right);
	}

	public static bool operator <(Personnummer? left, Personnummer? right)
	{
		return ReferenceEquals(left, null) ? !ReferenceEquals(right, null) : left.CompareTo(right) < 0;
	}

	public static bool operator <=(Personnummer left, Personnummer right)
	{
		return ReferenceEquals(left, null) || left.CompareTo(right) <= 0;
	}

	public static bool operator >(Personnummer left, Personnummer right)
	{
		return !ReferenceEquals(left, null) && left.CompareTo(right) > 0;
	}

	public static bool operator >=(Personnummer left, Personnummer right)
	{
		return ReferenceEquals(left, null) ? ReferenceEquals(right, null) : left.CompareTo(right) >= 0;
	}
}

public static class PersonnummerExtensions
{
	public static Personnummer? GetNullablePersonnummer(this string? value)
	{
		if (string.IsNullOrEmpty(value)) return null;
		return new Personnummer(value);
	}
}
