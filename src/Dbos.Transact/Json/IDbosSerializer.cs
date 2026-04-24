namespace Dbos.Transact.Json;

/// <summary>Marker interface for pluggable DBOS serializers. Full contract defined in a later wave.</summary>
public interface IDbosSerializer
{
    string Name { get; }
}
