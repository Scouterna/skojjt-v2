using Skojjt.Core.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Skojjt.Core.Tests;

[TestClass]
public class TestPersonnummer
{
	[TestMethod]
	public void TestSomeNumbers()
	{
		HashSet<int> hashes = new HashSet<int>();
		foreach (var s in TestPersonnummerData.s_testpersonnummer)
		{
			var personnummer = new Personnummer(s);
			var hash = personnummer.GetHashCode();
			Assert.IsTrue(personnummer.IsValid);
			Assert.AreEqual(s, personnummer.ToString());
			Assert.DoesNotContain(hash, hashes);
			hashes.Add(hash);
			Assert.Contains(hash, hashes);
		}
	}

	[TestMethod]
	public void TestHashCode()
	{
		var personnummer = new Personnummer("20000101-2384");
		Assert.IsTrue(personnummer.IsValid);
		var hash = personnummer.GetHashCode();
		Assert.AreEqual(2000101238, hash);

		personnummer = new Personnummer("19991231-2387");
		Assert.IsTrue(personnummer.IsValid);
		hash = personnummer.GetHashCode();
		Assert.AreEqual(1991231238, hash);
	}

	[TestMethod]
	public void TestEquals()
	{
		// test personnummer from skatteverket
		var pnr1 = new Personnummer("201512312396");
		var pnr2 = new Personnummer("201512312396");
		var pnr3 = new Personnummer("201010602397");
		Assert.IsTrue(pnr1.IsValid);
		Assert.IsTrue(pnr2.IsValid);
		Assert.IsTrue(pnr3.IsValid);

		Assert.IsTrue(pnr1.Equals(pnr2));
		Assert.IsTrue(pnr1.Equals(pnr2));

		Assert.IsFalse(pnr1.Equals(pnr3));
		Assert.IsFalse(pnr1.Equals(pnr3));

		Assert.IsTrue(pnr1.Equals((object)pnr1));
		Assert.IsTrue(pnr1.Equals((object)pnr2));
		object? nil = null;
		Assert.IsFalse(pnr1.Equals(nil));
		Assert.IsTrue(pnr1.Equals((object)pnr2));
		Assert.IsFalse(pnr1.Equals(null));
	}

	[TestMethod]
	public void TestCompare()
	{
		// test personnummer from skatteverket
		var pnr1 = new Personnummer("201512312396");
		var pnr2 = new Personnummer("201512312396");
		var pnr3 = new Personnummer("201010602397");
		Assert.IsTrue(pnr1.IsValid);
		Assert.IsTrue(pnr2.IsValid);
		Assert.IsTrue(pnr3.IsValid);

		Assert.IsTrue(pnr1 == pnr2);
		Assert.IsFalse(pnr1 != pnr2);

		Assert.IsTrue(pnr1 != pnr3);
		Assert.IsFalse(pnr1 == pnr3);
#pragma warning disable CS1718 // Comparison made to same variable
		Assert.IsTrue(pnr1 == pnr1);
#pragma warning restore CS1718 // Comparison made to same variable
#pragma warning disable CS8625 // Comparison made to same variable
		Assert.IsFalse(pnr1 == null);
		Assert.IsFalse(null == pnr1);
#pragma warning restore CS8625 // Comparison made to same variable

		Assert.IsTrue(pnr3 < pnr2);
		Assert.IsTrue(pnr2 > pnr3);

		Assert.IsTrue(pnr1 <= pnr2);
		Assert.IsTrue(pnr1 >= pnr2);

		Assert.IsTrue(pnr2 >= pnr3);
		Assert.IsFalse(pnr2 <= pnr3);
		Assert.IsFalse(pnr3 >= pnr2);
	}

	[TestMethod]
	public void TestErrors()
	{
		Assert.IsFalse(new Personnummer("20151x312396").IsValid);
		Assert.IsFalse(new Personnummer("1010602397").IsValid);
		Assert.IsFalse(new Personnummer("").IsValid);
		Assert.IsFalse(new Personnummer("2015123123961").IsValid);
	}

	[TestMethod]
	public void TestProperties()
	{
		Personnummer pnr;
		// test personnummer from skatteverket
		pnr = new Personnummer("20151231-2396");

		Assert.IsTrue(pnr.IsValid);
		Assert.IsFalse(pnr.IsSamordningsnummer);
		Assert.AreEqual(2015, pnr.Year);
		Assert.AreEqual(12, pnr.Month);
		Assert.AreEqual(31, pnr.Day);
		Assert.AreEqual(new DateOnly(2015, 12, 31), pnr.BirthDay);
		Assert.AreEqual("2015-12-31", pnr.BirthDayString);
		Assert.IsFalse(pnr.IsFemale);
		Assert.IsTrue(pnr.IsMale);

		pnr = new Personnummer("199001012385");
		Assert.IsTrue(pnr.IsValid);
		Assert.IsFalse(pnr.IsSamordningsnummer);
		Assert.AreEqual(1990, pnr.Year);
		Assert.AreEqual(1, pnr.Month);
		Assert.AreEqual(1, pnr.Day);
		Assert.IsTrue(pnr.IsFemale);
		Assert.IsFalse(pnr.IsMale);

		pnr = new Personnummer("198912312389");
		Assert.IsTrue(pnr.IsValid);
		Assert.IsFalse(pnr.IsSamordningsnummer);
		Assert.AreEqual(1989, pnr.Year);
		Assert.AreEqual(12, pnr.Month);
		Assert.AreEqual(31, pnr.Day);
		Assert.AreEqual("1989-12-31", pnr.BirthDayString);
		Assert.IsTrue(pnr.IsFemale);
		Assert.IsFalse(pnr.IsMale);

		pnr = new Personnummer("198912312369"); // wrong checksum
		Assert.IsFalse(pnr.IsValid);
		Assert.IsFalse(pnr.IsSamordningsnummer);
		Assert.AreEqual(1989, pnr.Year);
		Assert.AreEqual(12, pnr.Month);
		Assert.AreEqual(31, pnr.Day);
		Assert.IsTrue(pnr.IsFemale);
		Assert.IsFalse(pnr.IsMale);

		// test samordningsnummer from skatteverket
		pnr = new Personnummer("191401682396");
		Assert.IsTrue(pnr.IsValid);
		Assert.IsTrue(pnr.IsSamordningsnummer);
		Assert.AreEqual(1914, pnr.Year);
		Assert.AreEqual(1, pnr.Month);
		Assert.AreEqual(8, pnr.Day);
		Assert.AreEqual("1914-01-08", pnr.BirthDayString);
		Assert.IsFalse(pnr.IsFemale);
		Assert.IsTrue(pnr.IsMale);

		pnr = new Personnummer("197001782395");
		Assert.IsTrue(pnr.IsValid);
		Assert.IsTrue(pnr.IsSamordningsnummer);
		Assert.AreEqual(1970, pnr.Year);
		Assert.AreEqual(1, pnr.Month);
		Assert.AreEqual(18, pnr.Day);
		Assert.AreEqual("1970-01-18", pnr.BirthDayString);
		Assert.IsFalse(pnr.IsFemale);
		Assert.IsTrue(pnr.IsMale);

		pnr = new Personnummer("201010602397");
		Assert.IsTrue(pnr.IsValid);
		Assert.IsTrue(pnr.IsSamordningsnummer);
		Assert.AreEqual(2010, pnr.Year);
		Assert.AreEqual(10, pnr.Month);
		Assert.AreEqual(0, pnr.Day);
		Assert.IsFalse(pnr.IsFemale);
		Assert.IsTrue(pnr.IsMale);

		pnr = new Personnummer("202301622383");
		Assert.IsTrue(pnr.IsValid);
		Assert.IsTrue(pnr.IsSamordningsnummer);
		Assert.AreEqual(2023, pnr.Year);
		Assert.AreEqual(1, pnr.Month);
		Assert.AreEqual(2, pnr.Day);
		Assert.IsTrue(pnr.IsFemale);
		Assert.IsFalse(pnr.IsMale);
	}
}
