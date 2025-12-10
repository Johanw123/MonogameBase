using System.Numerics;
using System; // For Math.Log10 and Math.Floor

public static class NumberFormatter
{
  private static readonly string[] Suffixes =
  {
        "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc", // ... and so on
        // You can extend this list indefinitely with your desired suffixes
    };

  /// <summary>
  /// Formats an arbitrarily large number using abbreviations.
  /// </summary>
  /// <param name="value">The large BigInteger to format.</param>
  /// <returns>The abbreviated string (e.g., "1.23T", "987.65Qa").</returns>
  public static string AbbreviateBigNumber(BigInteger value)
  {
    // Handle negative numbers (optional, depending on your game)
    bool isNegative = value.Sign < 0;
    if (isNegative)
    {
      value = BigInteger.Abs(value);
    }

    // Handle small numbers immediately
    if (value < 1000)
    {
      return (isNegative ? "-" : "") + value.ToString();
    }

    // 1. Calculate the magnitude (power of 1000)
    // Log10 of the value, divided by 3, gives the index for the suffixes.
    // We use Math.Floor to get a clean integer index.
    int magnitude = (int)Math.Floor(BigInteger.Log10(value) / 3);

    // Cap the magnitude to the number of suffixes we have
    if (magnitude >= Suffixes.Length)
    {
      // If the number is too big for our suffixes, return the raw value
      // or return an error string like "A lot!"
      return value.ToString();
    }

    // 2. Get the divisor
    // Calculate 10^(3 * magnitude). Example: for M (magnitude=2), divisor is 1,000,000
    BigInteger divisor = BigInteger.Pow(10, magnitude * 3);

    // 3. Perform the division
    // To get a decimal value, we can perform integer division but shift the number first.
    // We want two decimal places, so we multiply by 100 before dividing by the main divisor.
    BigInteger shiftedValue = value * 100;
    BigInteger mainPart = shiftedValue / divisor;

    // 4. Extract parts and format
    // Get the whole number part of the abbreviation (e.g., the '1' in '1.23')
    long wholePart = (long)(mainPart / 100);

    // Get the fractional part (the '23' in '1.23')
    long fractionalPart = (long)(mainPart % 100);

    // Format: "WholePart.FractionalPartSuffix"
    string result = string.Format("{0}.{1:D2}{2}", wholePart, fractionalPart, Suffixes[magnitude]);

    return (isNegative ? "-" : "") + result;
  }
}
