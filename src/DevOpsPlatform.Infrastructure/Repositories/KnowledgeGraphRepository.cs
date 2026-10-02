namespace DevOpsPlatform.Infrastructure.Repositories;

using DevOpsPlatform.Core.Entities;
using DevOpsPlatform.Core.Enums;
using DevOpsPlatform.Core.Interfaces;
using DevOpsPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

public class KnowledgeGraphRepository : IKnowledgeGraphRepository
{
    private readonly PlatformDbContext _db;

    public KnowledgeGraphRepository(PlatformDbContext db) => _db = db;

    public async Task<KnowledgeNode?> GetByIdAsync(Ulid id, CancellationToken ct = default)
        => await _db.KnowledgeNodes
            .Include(n => n.Parent)
            .Include(n => n.Children)
            .FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task<KnowledgeNode?> GetBySlugAsync(string type, string slug, string? version = null, CancellationToken ct = default)
    {
        var query = _db.KnowledgeNodes.Where(n => n.Type.ToString() == type && n.Slug == slug);
        if (version != null) query = query.Where(n => n.Version == version);
        return await query.FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<KnowledgeNode>> GetChildrenAsync(Ulid parentId, CancellationToken ct = default)
        => await _db.KnowledgeNodes.Where(n => n.ParentId == parentId).OrderBy(n => n.Title).ToListAsync(ct);

    public async Task<IReadOnlyList<KnowledgeNode>> GetByTypeAsync(NodeType type, CancellationToken ct = default)
        => await _db.KnowledgeNodes.Where(n => n.Type == type).OrderBy(n => n.Title).ToListAsync(ct);

    public async Task<IReadOnlyList<KnowledgeEdge>> GetOutgoingEdgesAsync(Ulid nodeId, EdgeType? edgeType = null, CancellationToken ct = default)
    {
        var query = _db.KnowledgeEdges.Where(e => e.FromId == nodeId);
        if (edgeType.HasValue) query = query.Where(e => e.EdgeType == edgeType.Value);
        return await query.Include(e => e.ToNode).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<KnowledgeEdge>> GetIncomingEdgesAsync(Ulid nodeId, EdgeType? edgeType = null, CancellationToken ct = default)
    {
        var query = _db.KnowledgeEdges.Where(e => e.ToId == nodeId);
        if (edgeType.HasValue) query = query.Where(e => e.EdgeType == edgeType.Value);
        return await query.Include(e => e.FromNode).ToListAsync(ct);
    }

    public async Task<KnowledgeNode> AddAsync(KnowledgeNode node, CancellationToken ct = default)
    {
        _db.KnowledgeNodes.Add(node);
        await _db.SaveChangesAsync(ct);
        return node;
    }

    public async Task<KnowledgeNode> UpdateAsync(KnowledgeNode node, CancellationToken ct = default)
    {
        _db.KnowledgeNodes.Update(node);
        await _db.SaveChangesAsync(ct);
        return node;
    }

    public async Task AddEdgeAsync(KnowledgeEdge edge, CancellationToken ct = default)
    {
        _db.KnowledgeEdges.Add(edge);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveEdgeAsync(Ulid fromId, Ulid toId, EdgeType edgeType, CancellationToken ct = default)
    {
        var edge = await _db.KnowledgeEdges.FindAsync([fromId, toId, edgeType], ct);
        if (edge != null)
        {
            _db.KnowledgeEdges.Remove(edge);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<Ulid>> GetPrerequisitesAsync(Ulid nodeId, CancellationToken ct = default)
    {
        var edges = await _db.KnowledgeEdges
            .Where(e => e.ToId == nodeId && e.EdgeType == EdgeType.Prereq)
            .Select(e => e.FromId)
            .ToListAsync(ct);
        return edges;
    }

    public async Task<IReadOnlyList<Ulid>> GetDependentsAsync(Ulid nodeId, CancellationToken ct = default)
    {
        var edges = await _db.KnowledgeEdges
            .Where(e => e.FromId == nodeId && e.EdgeType == EdgeType.Prereq)
            .Select(e => e.ToId)
            .ToListAsync(ct);
        return edges;
    }
}
