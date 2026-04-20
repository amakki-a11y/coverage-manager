using Microsoft.AspNetCore.Mvc;
using CoverageManager.Core.Engines;
using CoverageManager.Core.Models;
using CoverageManager.Api.Services;

namespace CoverageManager.Api.Controllers;

/// <summary>
/// CRUD over <c>symbol_mappings</c> — the translation layer between raw B-Book
/// symbols (e.g. <c>XAUUSD-</c>), coverage/LP symbols (e.g. <c>GOLD</c>), and
/// the canonical name used everywhere else in the app (e.g. <c>XAUUSD</c>).
/// Any write refreshes <c>PositionManager</c>'s in-memory map so the next
/// broadcast reflects the change.
/// </summary>
[ApiController]
[Route("api/mappings")]
public class SymbolMappingController : ControllerBase
{
    private readonly PositionManager _positionManager;
    private readonly SupabaseService _supabase;

    public SymbolMappingController(PositionManager positionManager, SupabaseService supabase)
    {
        _positionManager = positionManager;
        _supabase = supabase;
    }

    /// <summary>
    /// GET /api/mappings — list all active mappings
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMappings()
    {
        var mappings = await _supabase.GetMappingsAsync();
        return Ok(mappings);
    }

    /// <summary>
    /// POST /api/mappings — add or update a mapping
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> UpsertMapping([FromBody] SymbolMapping mapping)
    {
        var result = await _supabase.UpsertMappingAsync(mapping);
        if (result == null) return StatusCode(500, "Failed to save mapping");

        // Refresh in-memory mappings
        var all = await _supabase.GetMappingsAsync();
        _positionManager.LoadMappings(all);

        return Ok(result);
    }

    /// <summary>
    /// PUT /api/mappings/{id} — update a mapping
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateMapping(Guid id, [FromBody] SymbolMapping mapping)
    {
        mapping.Id = id;
        var result = await _supabase.UpsertMappingAsync(mapping);
        if (result == null) return StatusCode(500, "Failed to update mapping");

        var all = await _supabase.GetMappingsAsync();
        _positionManager.LoadMappings(all);

        return Ok(result);
    }

    /// <summary>
    /// DELETE /api/mappings/{id} — delete a mapping
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteMapping(Guid id)
    {
        var success = await _supabase.DeleteMappingAsync(id);
        if (!success) return StatusCode(500, "Failed to delete mapping");

        var all = await _supabase.GetMappingsAsync();
        _positionManager.LoadMappings(all);

        return NoContent();
    }
}
