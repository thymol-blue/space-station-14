using System.Numerics;
using Content.Shared.Weather;
using Robust.Client.Graphics;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;

namespace Content.Client.Overlays;

public sealed partial class StencilOverlay
{
    private void DrawWeather(in OverlayDrawArgs args, WeatherPrototype weatherProto, float alpha, Matrix3 invMatrix)
    {
        var worldHandle = args.WorldHandle;
        var mapId = args.MapId;
        var worldAABB = args.WorldAABB;
        var worldBounds = args.WorldBounds;
        var position = args.Viewport.Eye?.Position.Position ?? Vector2.Zero;

        // Cut out the irrelevant bits via stencil
        // This is why we don't just use parallax; we might want specific tiles to get drawn over
        // particularly for planet maps or stations.
        worldHandle.RenderInRenderTarget(_blep!, () =>
        {
            var bodyQuery = _entManager.GetEntityQuery<PhysicsComponent>();
            var xformQuery = _entManager.GetEntityQuery<TransformComponent>();
            var weatherIgnoreQuery = _entManager.GetEntityQuery<IgnoreWeatherComponent>();

            // idk if this is safe to cache in a field and clear sloth help
            var grids = new List<Entity<MapGridComponent>>();
            _mapManager.FindGridsIntersecting(mapId, worldAABB, ref grids);

            foreach (var grid in grids)
            {
                var matrix = _transform.GetWorldMatrix(grid, xformQuery);
                Matrix3.Multiply(in matrix, in invMatrix, out var matty);
                worldHandle.SetTransform(matty);

                foreach (var tile in grid.Comp.GetTilesIntersecting(worldAABB))
                {
                    // Ignored tiles for stencil
                    if (_weather.CanWeatherAffect(grid, tile, weatherIgnoreQuery, bodyQuery))
                    {
                        continue;
                    }

                    var gridTile = new Box2(tile.GridIndices * grid.Comp.TileSize,
                        (tile.GridIndices + Vector2i.One) * grid.Comp.TileSize);

                    worldHandle.DrawRect(gridTile, Color.White);
                }
            }

        }, Color.Transparent);

        worldHandle.SetTransform(Matrix3.Identity);
        worldHandle.UseShader(_protoManager.Index<ShaderPrototype>("StencilMask").Instance());
        worldHandle.DrawTextureRect(_blep!.Texture, worldBounds);
        var curTime = _timing.RealTime;
        var sprite = _sprite.GetFrame(weatherProto.Sprite, curTime);

        // Draw the rain
        worldHandle.UseShader(_protoManager.Index<ShaderPrototype>("StencilDraw").Instance());
        _parallax.DrawParallax(worldHandle, worldAABB, sprite, curTime, position, Vector2.Zero, modulate: (weatherProto.Color ?? Color.White).WithAlpha(alpha));

        worldHandle.SetTransform(Matrix3.Identity);
        worldHandle.UseShader(null);
    }
}
