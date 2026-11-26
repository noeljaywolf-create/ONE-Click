using OneClick.Core.Provenance;
using OneClick.Core.TaggedValues;
using OneClick.Core.Units;

namespace OneClick.Core.Formulas;

/// <summary>
/// Minimal quantity contribution for Q01 aggregation. Ownership is explicit so assembly
/// contents can be deduplicated without silently excluding included items.
/// </summary>
public sealed record QuantityContribution
{
    public QuantityContribution(
        string ownerId,
        string ledger,
        string specGroup,
        double value,
        SiUnit unit,
        string? includedByOwnerId = null)
    {
        OwnerId = Guard.Required(ownerId, nameof(ownerId));
        Ledger = Guard.Required(ledger, nameof(Ledger));
        SpecGroup = Guard.Required(specGroup, nameof(SpecGroup));
        Value = Guard.Finite(value, nameof(value));
        if (!Enum.IsDefined(unit))
            throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unit must be a defined SI unit.");
        Unit = unit;
        IncludedByOwnerId = string.IsNullOrWhiteSpace(includedByOwnerId) ? null : includedByOwnerId;
    }

    public string OwnerId { get; }
    public string Ledger { get; }
    public string SpecGroup { get; }
    public double Value { get; }
    public SiUnit Unit { get; }

    /// <summary>
    /// When set, this contribution is included by the named assembly owner and must not be
    /// double-counted when that assembly owner is counted in the same ledger/spec/group.
    /// </summary>
    public string? IncludedByOwnerId { get; }

    /// <summary>
    /// Maps this Q01 aggregation input to the canonical domain-aligned contribution record.
    /// The caller supplies identity/provenance that the minimal aggregation record does not carry.
    /// Aggregation-only metadata (<see cref="IncludedByOwnerId"/>) is not part of the domain
    /// contribution contract and is intentionally not mapped.
    /// </summary>
    public OneClick.Core.Provenance.QuantityContribution ToProvenance(
        QuantityContributionId id,
        Ledger ledger,
        IReadOnlyList<string> sourceRefs,
        string methodVersion)
    {
        ArgumentNullException.ThrowIfNull(sourceRefs);
        Guard.Required(methodVersion, nameof(methodVersion));

        var qualifiedValue = new KnownValue(Value, Unit, sourceRefs, methodVersion);
        return new OneClick.Core.Provenance.QuantityContribution(
            id,
            OwnerId,
            ledger,
            SpecGroup,
            qualifiedValue,
            sourceRefs,
            methodVersion);
    }
}

public sealed record QuantityAggregateResult(
    KnownValue Total,
    IReadOnlyList<string> CountedOwnerIds,
    IReadOnlyList<string> ExcludedOwnerIds,
    IReadOnlyList<string> Warnings)
{
    public KnownValue Total { get; } = Total ?? throw new ArgumentNullException(nameof(Total));
    public IReadOnlyList<string> CountedOwnerIds { get; } = CountedOwnerIds ?? throw new ArgumentNullException(nameof(CountedOwnerIds));
    public IReadOnlyList<string> ExcludedOwnerIds { get; } = ExcludedOwnerIds ?? throw new ArgumentNullException(nameof(ExcludedOwnerIds));
    public IReadOnlyList<string> Warnings { get; } = Warnings ?? throw new ArgumentNullException(nameof(Warnings));
}

public sealed record PackQuantityResult(
    TaggedValue Packs,
    TaggedValue TotalUnits,
    TaggedValue Required,
    TaggedValue PackSize,
    string PackPolicy)
{
    public TaggedValue Packs { get; } = Packs ?? throw new ArgumentNullException(nameof(Packs));
    public TaggedValue TotalUnits { get; } = TotalUnits ?? throw new ArgumentNullException(nameof(TotalUnits));
    public TaggedValue Required { get; } = Required ?? throw new ArgumentNullException(nameof(Required));
    public TaggedValue PackSize { get; } = PackSize ?? throw new ArgumentNullException(nameof(PackSize));
    public string PackPolicy { get; } = Guard.Required(PackPolicy, nameof(PackPolicy));
}

public sealed record PipeMetalMassResult(
    TaggedValue MetalVolume,
    TaggedValue Mass)
{
    public TaggedValue MetalVolume { get; } = MetalVolume ?? throw new ArgumentNullException(nameof(MetalVolume));
    public TaggedValue Mass { get; } = Mass ?? throw new ArgumentNullException(nameof(Mass));
}

public sealed record StockLowerBoundResult(
    TaggedValue Sticks,
    TaggedValue TotalCutLength,
    KnownValue UsableStock,
    string Limitation)
{
    public TaggedValue Sticks { get; } = Sticks ?? throw new ArgumentNullException(nameof(Sticks));
    public TaggedValue TotalCutLength { get; } = TotalCutLength ?? throw new ArgumentNullException(nameof(TotalCutLength));
    public KnownValue UsableStock { get; } = UsableStock ?? throw new ArgumentNullException(nameof(UsableStock));
    public string Limitation { get; } = Guard.Required(Limitation, nameof(Limitation));
}

public static class QuantityFormulas
{
    public static FormulaDescriptor Q01 { get; } = new(
        Id: "Q01",
        MethodVersion: "Q01.1.0",
        OutputUnit: SiUnit.Meter, // Default/documented for length; actual result unit is validated from contributions.
        Formula: "sum(unique owned contributions by ledger/spec/group)",
        Applicability: "Sum unique owned contributions by ledger/spec/group; deduplicate assembly ownership.",
        FailureHandling: "Rejects mixed units, non-finite values, or ambiguous duplicate ownership with conflicting values; does not silently exclude included items.");

    public static FormulaDescriptor Q02 { get; } = new(
        Id: "Q02",
        MethodVersion: "Q02.1.0",
        OutputUnit: SiUnit.Dimensionless,
        Formula: "allowance=basis*w; packs=ceil(required/packSize)",
        Applicability: "Allowance and pack counting with explicit w and pack policy; unknown basis -> unknown; packSize>0.",
        FailureHandling: "Returns UnknownValue when basis/required is unknown; rejects non-finite/non-positive parameters, invalid policies, or non-finite results.");

    public static FormulaDescriptor Q03 { get; } = new(
        Id: "Q03",
        MethodVersion: "Q03.1.0",
        OutputUnit: SiUnit.Kilogram,
        Formula: "metalVolume=pi/4*(OD^2-ID^2)*L; mass=rho*volume",
        Applicability: "Uniform ideal pipe; OD>ID>0; L>0; rho>0; prefer qualified catalogue mass for real product.",
        FailureHandling: "Returns UnknownValue when any input is unknown; rejects invalid dimensions/density or non-finite results.");

    public static FormulaDescriptor Q04 { get; } = new(
        Id: "Q04",
        MethodVersion: "Q04.1.0",
        OutputUnit: SiUnit.Dimensionless,
        Formula: "ceil(sum(cutLengths)/usableStock)",
        Applicability: "Lower bound stock count from cut lengths and usable stock length; usableStock>0; finite non-negative cut lengths.",
        FailureHandling: "Returns UnknownValue when any included cut length is unknown; rejects non-finite/negative cut lengths or non-finite/non-positive usable stock. Result is a lower bound only and ignores fragmentation/kerf.");

    /// <summary>
    /// Q01 basic: sum unique owned contributions by ledger/spec/group.
    /// Deduplicates by owner within the requested group. Contributions included by a counted
    /// assembly owner are excluded from direct sum to prevent double counting, and this exclusion
    /// is reported explicitly.
    /// </summary>
    public static QuantityAggregateResult SumUniqueOwnedContributions(
        IEnumerable<QuantityContribution> contributions,
        string ledger,
        string specGroup,
        SiUnit unit,
        IReadOnlyList<string> sourceRefs)
    {
        return SumUniqueOwnedContributionsCore(contributions, ledger, specGroup, unit, sourceRefs).Result;
    }

    private static (QuantityAggregateResult Result, IReadOnlyList<QuantityContribution> DedupedInGroup) SumUniqueOwnedContributionsCore(
        IEnumerable<QuantityContribution> contributions,
        string ledger,
        string specGroup,
        SiUnit unit,
        IReadOnlyList<string> sourceRefs)
    {
        ArgumentNullException.ThrowIfNull(contributions);
        Guard.Required(ledger, nameof(ledger));
        Guard.Required(specGroup, nameof(specGroup));
        if (!Enum.IsDefined(unit))
            throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unit must be a defined SI unit.");
        ArgumentNullException.ThrowIfNull(sourceRefs);

        var inGroup = contributions
            .Where(c => string.Equals(c.Ledger, ledger, StringComparison.Ordinal))
            .Where(c => string.Equals(c.SpecGroup, specGroup, StringComparison.Ordinal))
            .ToList();

        foreach (var c in inGroup)
        {
            if (c.Unit != unit)
            {
                throw new InvalidOperationException(
                    $"Q01 mixed units in ledger '{ledger}', spec/group '{specGroup}': owner '{c.OwnerId}' has {c.Unit}, expected {unit}.");
            }
        }

        // Deduplicate by owner id. Same owner with same value is idempotent; conflicting value is ambiguous.
        var byOwner = new Dictionary<string, QuantityContribution>(StringComparer.Ordinal);
        foreach (var c in inGroup)
        {
            if (byOwner.TryGetValue(c.OwnerId, out var existing))
            {
                if (existing.Value != c.Value || existing.IncludedByOwnerId != c.IncludedByOwnerId)
                {
                    throw new InvalidOperationException(
                        $"Q01 ambiguous duplicate ownership for owner '{c.OwnerId}' in ledger '{ledger}', spec/group '{specGroup}'.");
                }

                continue; // identical duplicate contribution
            }

            byOwner.Add(c.OwnerId, c);
        }

        var dedupedInGroup = byOwner.Values.OrderBy(c => c.OwnerId, StringComparer.Ordinal).ToArray();
        var owners = byOwner.Keys.ToHashSet(StringComparer.Ordinal);
        var counted = new List<string>();
        var excluded = new List<string>();
        var warnings = new List<string>();
        double total = 0d;

        foreach (var c in dedupedInGroup)
        {
            if (c.IncludedByOwnerId is not null && owners.Contains(c.IncludedByOwnerId))
            {
                excluded.Add(c.OwnerId);
                warnings.Add($"excluded:owner:{c.OwnerId}:included-by:{c.IncludedByOwnerId}");
                continue;
            }

            counted.Add(c.OwnerId);
            total = Guard.Finite(total + c.Value, nameof(total));
        }

        var totalValue = new KnownValue(total, unit, sourceRefs, Q01.MethodVersion);
        return (new QuantityAggregateResult(totalValue, counted, excluded, warnings), dedupedInGroup);
    }

    /// <summary>Q02: allowance = basis * w. Unknown basis -> unknown. w and policy must be explicit.</summary>
    public static TaggedValue Allowance(
        TaggedValue basis,
        double w,
        string wPolicy,
        IReadOnlyList<string> sourceRefs)
    {
        ArgumentNullException.ThrowIfNull(basis);
        Guard.Required(wPolicy, nameof(wPolicy));
        ArgumentNullException.ThrowIfNull(sourceRefs);
        Guard.Finite(w, nameof(w));
        if (w < 0d)
            throw new ArgumentOutOfRangeException(nameof(w), w, "Q02 allowance factor w must be >= 0.");

        if (basis is UnknownValue)
        {
            return new UnknownValue(
                basis.Unit,
                sourceRefs,
                Q02.MethodVersion,
                "Unknown basis -> unknown allowance.",
                ["basis:unknown"]);
        }

        if (basis is not KnownValue knownBasis)
            throw new ArgumentException("Basis must be a known or unknown tagged value.", nameof(basis));

        var allowance = Guard.Finite(knownBasis.Value * w, "allowance");
        return new KnownValue(allowance, knownBasis.Unit, sourceRefs, Q02.MethodVersion);
    }

    /// <summary>Q02: packs = ceil(required/packSize), totalUnits = packs * packSize. Unknown required -> unknown.</summary>
    public static PackQuantityResult Packs(
        TaggedValue required,
        double packSize,
        string packPolicy,
        IReadOnlyList<string> sourceRefs)
    {
        ArgumentNullException.ThrowIfNull(required);
        Guard.Required(packPolicy, nameof(packPolicy));
        ArgumentNullException.ThrowIfNull(sourceRefs);
        Guard.Finite(packSize, nameof(packSize));
        if (packSize <= 0d)
            throw new ArgumentOutOfRangeException(nameof(packSize), packSize, "Q02 packSize must be > 0.");

        if (required is UnknownValue)
        {
            var unknownPacks = new UnknownValue(
                SiUnit.Dimensionless,
                sourceRefs,
                Q02.MethodVersion,
                "Unknown required basis -> unknown pack count.",
                ["required:unknown"]);
            var unknownTotal = new UnknownValue(
                required.Unit,
                sourceRefs,
                Q02.MethodVersion,
                "Unknown required basis -> unknown total units.",
                ["required:unknown"]);
            var unknownPackSize = new UnknownValue(
                required.Unit,
                sourceRefs,
                Q02.MethodVersion,
                "Unknown required basis -> unknown pack size context.",
                ["required:unknown"]);
            return new PackQuantityResult(unknownPacks, unknownTotal, required, unknownPackSize, packPolicy);
        }

        if (required is not KnownValue knownRequired)
            throw new ArgumentException("Required must be a known or unknown tagged value.", nameof(required));
        if (knownRequired.Value < 0d)
            throw new ArgumentOutOfRangeException(nameof(required), knownRequired.Value, "Q02 required must be >= 0.");

        var rawPacks = Guard.Finite(Math.Ceiling(knownRequired.Value / packSize), "packs");
        if (rawPacks > int.MaxValue)
            throw new OverflowException("Q02 pack count exceeds supported integer range.");

        var packs = (int)rawPacks;
        var totalUnits = Guard.Finite(packs * packSize, "totalUnits");

        return new PackQuantityResult(
            Packs: new KnownValue(packs, SiUnit.Dimensionless, sourceRefs, Q02.MethodVersion),
            TotalUnits: new KnownValue(totalUnits, knownRequired.Unit, sourceRefs, Q02.MethodVersion),
            Required: knownRequired,
            PackSize: new KnownValue(packSize, knownRequired.Unit, sourceRefs, Q02.MethodVersion),
            PackPolicy: packPolicy);
    }

    /// <summary>
    /// Q03: metalVolume=pi/4*(OD^2-ID^2)*L; mass=rho*volume.
    /// Uniform ideal pipe; prefer qualified catalogue mass for real product.
    /// </summary>
    public static PipeMetalMassResult PipeMetalMass(
        TaggedValue outerDiameterM,
        TaggedValue innerDiameterM,
        TaggedValue lengthM,
        TaggedValue densityKgPerM3,
        IReadOnlyList<string> sourceRefs)
    {
        ArgumentNullException.ThrowIfNull(outerDiameterM);
        ArgumentNullException.ThrowIfNull(innerDiameterM);
        ArgumentNullException.ThrowIfNull(lengthM);
        ArgumentNullException.ThrowIfNull(densityKgPerM3);
        ArgumentNullException.ThrowIfNull(sourceRefs);

        var issues = new List<string>();
        CollectUnknownIssue(outerDiameterM, "od:unknown", issues);
        CollectUnknownIssue(innerDiameterM, "id:unknown", issues);
        CollectUnknownIssue(lengthM, "length:unknown", issues);
        CollectUnknownIssue(densityKgPerM3, "density:unknown", issues);
        if (issues.Count > 0)
        {
            return new PipeMetalMassResult(
                new UnknownValue(SiUnit.CubicMeter, sourceRefs, Q03.MethodVersion, "Unknown input -> unknown pipe metal volume.", issues),
                new UnknownValue(SiUnit.Kilogram, sourceRefs, Q03.MethodVersion, "Unknown input -> unknown pipe mass.", issues));
        }

        var od = RequireKnown(outerDiameterM, SiUnit.Meter, nameof(outerDiameterM));
        var id = RequireKnown(innerDiameterM, SiUnit.Meter, nameof(innerDiameterM));
        var l = RequireKnown(lengthM, SiUnit.Meter, nameof(lengthM));
        var rho = RequireKnown(densityKgPerM3, SiUnit.KilogramPerCubicMeter, nameof(densityKgPerM3));

        if (od <= 0d)
            throw new ArgumentOutOfRangeException(nameof(outerDiameterM), od, "Q03 requires OD > 0.");
        if (id <= 0d)
            throw new ArgumentOutOfRangeException(nameof(innerDiameterM), id, "Q03 requires ID > 0.");
        if (!(od > id))
            throw new ArgumentOutOfRangeException(nameof(innerDiameterM), id, "Q03 requires OD > ID > 0.");
        if (l <= 0d)
            throw new ArgumentOutOfRangeException(nameof(lengthM), l, "Q03 requires L > 0.");
        if (rho <= 0d)
            throw new ArgumentOutOfRangeException(nameof(densityKgPerM3), rho, "Q03 requires rho > 0.");

        var metalVolume = Guard.Finite((Math.PI / 4d) * ((od * od) - (id * id)) * l, "metalVolume");
        if (metalVolume <= 0d)
            throw new InvalidOperationException("Q03 produced non-positive metal volume for valid-looking inputs.");

        var mass = Guard.Finite(rho * metalVolume, "mass");

        return new PipeMetalMassResult(
            new KnownValue(metalVolume, SiUnit.CubicMeter, sourceRefs, Q03.MethodVersion),
            new KnownValue(mass, SiUnit.Kilogram, sourceRefs, Q03.MethodVersion));
    }

    private static void CollectUnknownIssue(TaggedValue value, string issue, List<string> issues)
    {
        if (value is UnknownValue)
            issues.Add(issue);
        else if (value is not KnownValue)
            throw new ArgumentException("Value must be a known or unknown tagged value.");
    }

    private static double RequireKnown(TaggedValue value, SiUnit expectedUnit, string paramName)
    {
        if (value is not KnownValue known)
            throw new ArgumentException($"Expected known {expectedUnit} value.", paramName);
        if (known.Unit != expectedUnit)
            throw new ArgumentException($"Expected unit {expectedUnit} but got {known.Unit}.", paramName);
        return known.Value;
    }

    /// <summary>
    /// Q04: lower bound stock count. ceil(sum(cutLengths)/usableStock).
    /// Lower bound only - ignores fragmentation/kerf and does not prove feasible packing.
    /// </summary>
    public static StockLowerBoundResult StockLengthLowerBound(
        IReadOnlyList<TaggedValue> cutLengths,
        double usableStockM,
        IReadOnlyList<string> sourceRefs)
    {
        ArgumentNullException.ThrowIfNull(cutLengths);
        ArgumentNullException.ThrowIfNull(sourceRefs);
        Guard.Finite(usableStockM, nameof(usableStockM));
        if (usableStockM <= 0d)
            throw new ArgumentOutOfRangeException(nameof(usableStockM), usableStockM, "Q04 requires usableStock > 0.");

        const string limitation = "Lower bound only; ignores fragmentation/kerf and does not prove feasible packing.";
        var usableStock = new KnownValue(usableStockM, SiUnit.Meter, sourceRefs, Q04.MethodVersion);

        var issues = new List<string>();
        double total = 0d;
        for (var i = 0; i < cutLengths.Count; i++)
        {
            var cut = cutLengths[i] ?? throw new ArgumentNullException(nameof(cutLengths), $"Cut length at index {i} is null.");
            if (cut is UnknownValue)
            {
                issues.Add($"cut:unknown:index:{i}");
                continue;
            }

            if (cut is not KnownValue known)
                throw new ArgumentException($"Cut length at index {i} must be a known or unknown tagged value.", nameof(cutLengths));
            if (known.Unit != SiUnit.Meter)
                throw new ArgumentException($"Cut length at index {i} must be in meters, got {known.Unit}.", nameof(cutLengths));
            if (known.Value < 0d)
                throw new ArgumentOutOfRangeException(nameof(cutLengths), known.Value, $"Q04 cut length at index {i} must be >= 0.");
            total = Guard.Finite(total + known.Value, "totalCutLength");
        }

        if (issues.Count > 0)
        {
            return new StockLowerBoundResult(
                new UnknownValue(SiUnit.Dimensionless, sourceRefs, Q04.MethodVersion,
                    "Unknown cut length in included scope -> unknown stock lower bound.", issues),
                new UnknownValue(SiUnit.Meter, sourceRefs, Q04.MethodVersion,
                    "Unknown cut length in included scope -> unknown total cut length.", issues),
                usableStock,
                limitation);
        }

        var rawSticks = Guard.Finite(Math.Ceiling(total / usableStockM), "sticks");
        if (rawSticks > int.MaxValue)
            throw new OverflowException("Q04 stick count exceeds supported integer range.");

        var sticks = (int)rawSticks;
        return new StockLowerBoundResult(
            new KnownValue(sticks, SiUnit.Dimensionless, sourceRefs, Q04.MethodVersion),
            new KnownValue(total, SiUnit.Meter, sourceRefs, Q04.MethodVersion),
            usableStock,
            limitation);
    }

    // --- Provenance-carrying overloads (Q01-Q04) ---

    public static DerivedResult<QuantityAggregateResult> SumUniqueOwnedContributions(
        IEnumerable<QuantityContribution> contributions,
        string ledger,
        string specGroup,
        SiUnit unit,
        IReadOnlyList<string> sourceRefs,
        IReadOnlyList<InputReference> inputs,
        IReadOnlyList<QuantityContributionId> contributionIds)
    {
        var value = SumUniqueOwnedContributions(contributions, ledger, specGroup, unit, sourceRefs);
        return FormulaProvenance.Wrap(value, value.Total.MethodVersion, inputs, value.Total.SourceRefs, contributionIds);
    }

    /// <summary>
    /// Q01 with canonical result envelope. Sums contributions, maps deduped in-group aggregation
    /// inputs to canonical <see cref="OneClick.Core.Provenance.QuantityContribution"/> records via
    /// <see cref="QuantityContributionAdapter"/>, and returns a <see cref="CalculationResult{T}"/>
    /// carrying provenance, aggregate warnings, and canonical contributions.
    /// </summary>
    public static CalculationResult<QuantityAggregateResult> SumUniqueOwnedContributions(
        IEnumerable<QuantityContribution> contributions,
        string ledger,
        string specGroup,
        SiUnit unit,
        IReadOnlyList<string> sourceRefs,
        IReadOnlyList<InputReference> inputs,
        Func<QuantityContribution, QuantityContributionId> contributionIdFactory)
    {
        ArgumentNullException.ThrowIfNull(contributionIdFactory);

        var (value, dedupedInGroup) = SumUniqueOwnedContributionsCore(contributions, ledger, specGroup, unit, sourceRefs);
        var canonicalLedger = QuantityContributionAdapter.ParseLedger(ledger);
        var canonicalContributions = QuantityContributionAdapter.ToCanonical(
            dedupedInGroup,
            canonicalLedger,
            sourceRefs,
            Q01.MethodVersion,
            contributionIdFactory);

        var contributionIds = canonicalContributions.Select(c => c.Id).ToArray();
        var issues = new List<string>();
        if (value.ExcludedOwnerIds.Count > 0)
            issues.Add($"q01:excluded-owners:{value.ExcludedOwnerIds.Count}");

        return FormulaProvenance.WrapCalculation(
            value,
            value.Total.MethodVersion,
            inputs,
            value.Total.SourceRefs,
            contributionIds,
            warnings: value.Warnings,
            issues: issues,
            contributions: canonicalContributions);
    }

    public static DerivedResult<TaggedValue> Allowance(
        TaggedValue basis,
        double w,
        string wPolicy,
        IReadOnlyList<string> sourceRefs,
        IReadOnlyList<InputReference> inputs,
        IReadOnlyList<QuantityContributionId> contributionIds)
    {
        var value = Allowance(basis, w, wPolicy, sourceRefs);
        return FormulaProvenance.WrapTagged(value, inputs, contributionIds);
    }

    public static DerivedResult<PackQuantityResult> Packs(
        TaggedValue required,
        double packSize,
        string packPolicy,
        IReadOnlyList<string> sourceRefs,
        IReadOnlyList<InputReference> inputs,
        IReadOnlyList<QuantityContributionId> contributionIds)
    {
        var value = Packs(required, packSize, packPolicy, sourceRefs);
        return FormulaProvenance.Wrap(value, value.Packs.MethodVersion, inputs, value.Packs.SourceRefs, contributionIds);
    }

    public static DerivedResult<PipeMetalMassResult> PipeMetalMass(
        TaggedValue outerDiameterM,
        TaggedValue innerDiameterM,
        TaggedValue lengthM,
        TaggedValue densityKgPerM3,
        IReadOnlyList<string> sourceRefs,
        IReadOnlyList<InputReference> inputs,
        IReadOnlyList<QuantityContributionId> contributionIds)
    {
        var value = PipeMetalMass(outerDiameterM, innerDiameterM, lengthM, densityKgPerM3, sourceRefs);
        return FormulaProvenance.Wrap(value, value.MetalVolume.MethodVersion, inputs, value.MetalVolume.SourceRefs, contributionIds);
    }

    public static DerivedResult<StockLowerBoundResult> StockLengthLowerBound(
        IReadOnlyList<TaggedValue> cutLengths,
        double usableStockM,
        IReadOnlyList<string> sourceRefs,
        IReadOnlyList<InputReference> inputs,
        IReadOnlyList<QuantityContributionId> contributionIds)
    {
        var value = StockLengthLowerBound(cutLengths, usableStockM, sourceRefs);
        return FormulaProvenance.Wrap(value, value.Sticks.MethodVersion, inputs, value.Sticks.SourceRefs, contributionIds);
    }
}
