using UnityEngine;

// How shut-in a cell is. Smell and appliance noise no longer use a flat indoor cut:
// Better Ears dampens each wall, window and shut door on the way to a zombie, and an open door does not.
public static class HSIndoor
{
    public static float SmellScale(World world, Vector3 at)
    {
        if (world == null)
            return 1f;
        Vector3i b = World.worldToBlockPos(at + new Vector3(0f, 0.5f, 0f));
        if (Enclosed(world, b) < 0.2f)
            return 1f;
        if (OpenLeak(world, b))
            return HSWorkingStuffSettings.OpenDoorSmell;
        return HSWorkingStuffSettings.IndoorSmell;
    }

    // 0 = open sky, 1 = no sun at the cell (inside a roofed room).
    public static float Enclosed(World world, Vector3i b)
    {
        IChunk chunk = world.GetChunkFromWorldPos(b);
        if (chunk == null)
            return 0f;
        int sun = chunk.GetLight(b.x, b.y, b.z, Chunk.LIGHT_TYPE.SUN);
        int sunUp = b.y < 255 ? chunk.GetLight(b.x, b.y + 1, b.z, Chunk.LIGHT_TYPE.SUN) : sun;
        int best = sun > sunUp ? sun : sunUp;
        return 1f - best / 15f;
    }

    public static bool OpenLeak(World world, Vector3i c)
    {
        for (int y = -1; y <= 2; y++)
        {
            for (int x = -2; x <= 2; x++)
            {
                for (int z = -2; z <= 2; z++)
                {
                    if (x == 0 && y == 0 && z == 0)
                        continue;
                    if (IsOpenOpening(world, new Vector3i(c.x + x, c.y + y, c.z + z)))
                        return true;
                }
            }
        }
        return false;
    }

    static bool IsOpenOpening(World world, Vector3i pos)
    {
        BlockValue bv = world.GetBlock(pos);
        if (bv.isair)
            return false;
        Block block = bv.Block;
        if (block == null)
            return false;
        if (block.isMultiBlock && bv.ischild)
        {
            pos = block.multiBlockPos.GetParentPos(pos, bv);
            bv = world.GetBlock(pos);
            block = bv.Block;
            if (block == null || bv.isair)
                return false;
        }
        if (block.HasTag(BlockTags.Window))
            return true;
        if (!(block.HasTag(BlockTags.Door) || block.HasTag(BlockTags.ClosetDoor) || block is BlockPoweredDoor))
            return false;
        if (block is BlockPoweredDoor)
            return BlockPoweredDoor.IsDoorOpen(bv.meta);
        TileEntity te = world.GetTileEntity(pos);
        TEFeatureDoor door;
        if (te != null && te.TryGetSelfOrFeature(out door))
            return door.IsOpen();
        return BlockPoweredDoor.IsDoorOpen(bv.meta);
    }
}
