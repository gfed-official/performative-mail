namespace PerformativeMail.Net.Tests.Soak;

public sealed class HashTrace
{
    private readonly List<HashWitness> _witnesses = new();

    public IReadOnlyList<HashWitness> Witnesses => _witnesses;

    public void Record(HashWitness witness)
    {
        if (witness is null)
            throw new ArgumentNullException(nameof(witness));

        _witnesses.Add(witness);
    }

    public HashVerdict Check(HashWitness witness)
    {
        if (witness is null)
            throw new ArgumentNullException(nameof(witness));

        for (int i = 0; i < witness.ViewerHashes.Count; i++)
        {
            var (seat, hash) = witness.ViewerHashes[i];
            if (hash != witness.ServerHash)
                return new HashVerdict.HashMismatch(seat, witness.ServerHash, hash);
        }

        return HashVerdict.Match.Instance;
    }
}
