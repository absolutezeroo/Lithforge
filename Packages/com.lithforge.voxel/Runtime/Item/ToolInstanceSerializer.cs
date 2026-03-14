using System.IO;
using Lithforge.Core.Data;

namespace Lithforge.Voxel.Item
{
    public static class ToolInstanceSerializer
    {
        private const ushort Version = 1;

        public static byte[] Serialize(ToolInstance tool)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter w = new BinaryWriter(ms))
            {
                w.Write(Version);
                w.Write((byte)tool.ToolType);
                w.Write((byte)tool.Parts.Length);
                for (int p = 0; p < tool.Parts.Length; p++)
                {
                    ToolPart part = tool.Parts[p];
                    w.Write((byte)part.PartType);
                    w.Write(part.MaterialId.ToString());
                    w.Write(part.SpeedContribution);
                    w.Write(part.DurabilityContribution);
                    w.Write(part.DamageContribution);
                    w.Write(part.DurabilityMultiplier);
                    w.Write(part.SpeedMultiplier);
                    w.Write((byte)part.TraitIds.Length);
                    for (int t = 0; t < part.TraitIds.Length; t++)
                    {
                        w.Write(part.TraitIds[t].ToString());
                    }
                }

                w.Write(tool.CurrentDurability);
                w.Write(tool.MaxDurability);
                w.Write(tool.BaseSpeed);
                w.Write(tool.BaseDamage);
                w.Write(tool.EffectiveToolLevel);
                w.Write((byte)tool.Slots.Length);
                for (int s = 0; s < tool.Slots.Length; s++)
                {
                    ModifierSlot slot = tool.Slots[s];
                    w.Write(slot.IsOccupied);
                    if (slot.IsOccupied)
                    {
                        w.Write(slot.ModifierId.ToString());
                        w.Write(slot.Level);
                    }
                }

                return ms.ToArray();
            }
        }

        public static ToolInstance Deserialize(byte[] data)
        {
            using (MemoryStream ms = new MemoryStream(data))
            using (BinaryReader r = new BinaryReader(ms))
            {
                ushort ver = r.ReadUInt16();
                ToolInstance tool = new ToolInstance();
                tool.ToolType = (ToolType)r.ReadByte();
                int partCount = r.ReadByte();
                tool.Parts = new ToolPart[partCount];
                for (int i = 0; i < partCount; i++)
                {
                    ToolPart part = ToolPart.Empty;
                    part.PartType = (ToolPartType)r.ReadByte();
                    part.MaterialId = ResourceId.Parse(r.ReadString());
                    part.SpeedContribution = r.ReadSingle();
                    part.DurabilityContribution = r.ReadInt32();
                    part.DamageContribution = r.ReadSingle();
                    part.DurabilityMultiplier = r.ReadSingle();
                    part.SpeedMultiplier = r.ReadSingle();
                    int traitCount = r.ReadByte();
                    part.TraitIds = new ResourceId[traitCount];
                    for (int t = 0; t < traitCount; t++)
                    {
                        part.TraitIds[t] = ResourceId.Parse(r.ReadString());
                    }

                    tool.Parts[i] = part;
                }

                tool.CurrentDurability = r.ReadInt32();
                tool.MaxDurability = r.ReadInt32();
                tool.BaseSpeed = r.ReadSingle();
                tool.BaseDamage = r.ReadSingle();
                tool.EffectiveToolLevel = r.ReadInt32();
                int slotCount = r.ReadByte();
                tool.Slots = new ModifierSlot[slotCount];
                for (int i = 0; i < slotCount; i++)
                {
                    bool occ = r.ReadBoolean();
                    if (occ)
                    {
                        tool.Slots[i] = new ModifierSlot
                        {
                            IsOccupied = true,
                            ModifierId = ResourceId.Parse(r.ReadString()),
                            Level = r.ReadInt32(),
                        };
                    }
                }

                return tool;
            }
        }
    }
}
