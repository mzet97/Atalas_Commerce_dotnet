namespace Atlas.Offers.Domain;

/// <summary>
/// Dinheiro em unidade mínima (minor units) com moeda explícita — INV-MONEY-001.
/// Nunca usar float/double para valor comercial; arredondamento explícito por regra.
/// Consolidar em pacote versionado compartilhado quando o repo de contratos publicar
/// pacotes (decisão registrada na SPEC-002); por ora cada serviço mantém sua cópia pura.
/// </summary>
public readonly partial record struct Money
{
    private static readonly Dictionary<string, int> Exponents = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BRL"] = 2, ["USD"] = 2, ["EUR"] = 2, ["JPY"] = 0,
    };

    public long AmountMinor { get; }
    public string Currency { get; }

    private Money(long amountMinor, string currency)
    {
        AmountMinor = amountMinor;
        Currency = currency;
    }

    public static Money Create(long amountMinor, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3 ||
            !currency.All(char.IsLetter))
            throw new ArgumentException($"Moeda inválida: '{currency}' (ISO 4217, 3 letras)");
        if (amountMinor < 0)
            throw new ArgumentOutOfRangeException(nameof(amountMinor), "Valor monetário não pode ser negativo");
        return new Money(amountMinor, currency.ToUpperInvariant());
    }

    public int Exponent => Exponents.TryGetValue(Currency, out var e) ? e : 2;

    public static Money FromMajor(decimal majorAmount, string currency)
    {
        var money = Create(0, currency);
        var rounded = decimal.Round(majorAmount, money.Exponent, MidpointRounding.ToEven);
        var minor = rounded * Pow10(money.Exponent);
        return Create(checked((long)decimal.Round(minor, 0, MidpointRounding.ToEven)), currency);
    }

    /// <summary>Proporção (ex.: desconto) com arredondamento bancário — soma de componentes deve reproduzir o total.</summary>
    public Money Multiply(decimal factor) =>
        new(checked((long)decimal.Round(AmountMinor * factor, 0, MidpointRounding.ToEven)), Currency);

    public Money Add(Money other) =>
        other.Currency == Currency
            ? new Money(checked(AmountMinor + other.AmountMinor), Currency)
            : throw new InvalidOperationException($"Moedas incompatíveis: {Currency} × {other.Currency}");

    private static decimal Pow10(int exponent) => exponent switch
    {
        0 => 1m,
        1 => 10m,
        2 => 100m,
        _ => throw new NotSupportedException($"Expoente não mapeado: {exponent}"),
    };
}
