public static class NumberExtensions
{
    public static string FormatLargeNumber(this long number)
    {
        const long Quintillion = 1_000_000_000_000_000_000;
        const long Quadrillion = 1_000_000_000_000_000;
        const long Trillion = 1_000_000_000_000;
        const long Billion = 1_000_000_000;
        const long Million = 1_000_000;
        const long Thousand = 1_000;

        if (number >= Quintillion)
        {
            return $"{(double)number / Quintillion:F2} Quint";
        }
        else if (number >= Quadrillion)
        {
            return $"{(double)number / Quadrillion:F2} Quadr";
        }
        else if (number >= Trillion)
        {
            return $"{(double)number / Trillion:F2} Tril";
        }
        else if (number >= Billion)
        {
            return $"{(double)number / Billion:F2} Bil";
        }
        else if (number >= Million)
        {
            return $"{(double)number / Million:F2} Mil";
        }
        else if (number >= Thousand)
        {
            return $"{(double)number / Thousand:F2} K";
        }
        else
        {
            return number.ToString("N0");
        }
    }
}
