using System;
using System.Text;

using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Server-to-Client full inventory snapshot for reconciliation.
    ///     Wire format: [StateId:4][SlotCount:1][per-slot: SlotIndex:1, NsLen:1, Ns:N,
    ///     NameLen:1, Name:N, Count:2, Durability:2, ComponentCount:1,
    ///     per-comp: TypeId:2, DataLen:2, Data:N]
    ///     [HasCursor:1][if HasCursor: NsLen:1, Ns:N, NameLen:1, Name:N, Count:2, Durability:2]
    /// </summary>
    public struct InventorySyncMessage : INetworkMessage
    {
        /// <summary>Minimum size: StateId(4) + SlotCount(1) + HasCursor(1).</summary>
        public const int MinSize = 6;

        /// <summary>The server's current inventory state ID after this mutation.</summary>
        public uint StateId;

        /// <summary>Serialized slot data. Each entry is: slotIndex, namespace, name, count, durability, components.</summary>
        public SyncSlot[] Slots;

        /// <summary>True if the message includes a cursor (held item) state.</summary>
        public bool HasCursor;

        /// <summary>Cursor item namespace. Empty if no cursor.</summary>
        public string CursorNs;

        /// <summary>Cursor item name. Empty if no cursor.</summary>
        public string CursorName;

        /// <summary>Cursor item count. 0 if no cursor.</summary>
        public ushort CursorCount;

        /// <summary>Cursor item durability.</summary>
        public short CursorDurability;

        /// <summary>Returns the MessageType for this message.</summary>
        public MessageType Type
        {
            get { return MessageType.InventorySync; }
        }

        /// <summary>Returns the total serialized payload size.</summary>
        public int GetSerializedSize()
        {
            int size = MinSize;
            int slotCount = Slots?.Length ?? 0;

            for (int i = 0; i < slotCount; i++)
            {
                if (Slots != null)
                {
                    size += GetSlotSize(ref Slots[i]);
                }
            }

            if (HasCursor && CursorCount > 0)
            {
                int nsLen = string.IsNullOrEmpty(CursorNs)
                    ? 0
                    : Encoding.UTF8.GetByteCount(CursorNs);
                int nameLen = string.IsNullOrEmpty(CursorName)
                    ? 0
                    : Encoding.UTF8.GetByteCount(CursorName);
                size += 1 + nsLen + 1 + nameLen + 2 + 2;
            }

            return size;
        }

        /// <summary>Writes the message payload into the buffer.</summary>
        public int Serialize(byte[] buffer, int offset)
        {
            int start = offset;
            MessageSerializer.WriteUInt(buffer, offset, StateId);
            offset += 4;

            int slotCount = Slots?.Length ?? 0;
            buffer[offset] = (byte)slotCount;
            offset += 1;

            for (int i = 0; i < slotCount; i++)
            {
                if (Slots != null)
                {
                    offset += SerializeSlot(ref Slots[i], buffer, offset);
                }
            }

            buffer[offset] = (byte)(HasCursor && CursorCount > 0 ? 1 : 0);
            offset += 1;

            if (HasCursor && CursorCount > 0)
            {
                if (string.IsNullOrEmpty(CursorNs))
                {
                    buffer[offset] = 0;
                    offset += 1;
                }
                else
                {
                    byte[] nsBytes = Encoding.UTF8.GetBytes(CursorNs);
                    buffer[offset] = (byte)nsBytes.Length;
                    offset += 1;
                    Array.Copy(nsBytes, 0, buffer, offset, nsBytes.Length);
                    offset += nsBytes.Length;
                }

                if (string.IsNullOrEmpty(CursorName))
                {
                    buffer[offset] = 0;
                    offset += 1;
                }
                else
                {
                    byte[] nameBytes = Encoding.UTF8.GetBytes(CursorName);
                    buffer[offset] = (byte)nameBytes.Length;
                    offset += 1;
                    Array.Copy(nameBytes, 0, buffer, offset, nameBytes.Length);
                    offset += nameBytes.Length;
                }

                MessageSerializer.WriteUShort(buffer, offset, CursorCount);
                offset += 2;
                MessageSerializer.WriteUShort(buffer, offset, (ushort)CursorDurability);
                offset += 2;
            }

            return offset - start;
        }

        /// <summary>Reads the message from the buffer.</summary>
        public static InventorySyncMessage Deserialize(byte[] buffer, int offset, int length)
        {
            InventorySyncMessage msg = new();
            int end = offset + length;

            if (length < MinSize)
            {
                return msg;
            }

            msg.StateId = MessageSerializer.ReadUInt(buffer, offset);
            offset += 4;

            int slotCount = buffer[offset];
            offset += 1;

            msg.Slots = new SyncSlot[slotCount];

            for (int i = 0; i < slotCount && offset < end; i++)
            {
                msg.Slots[i] = DeserializeSlot(buffer, ref offset, end);
            }

            // Cursor field (optional for backward compatibility)
            if (offset < end)
            {
                byte hasCursor = buffer[offset];
                offset += 1;

                if (hasCursor != 0 && offset < end)
                {
                    msg.HasCursor = true;

                    int nsLen = buffer[offset];
                    offset += 1;

                    if (nsLen > 0 && offset + nsLen <= end)
                    {
                        msg.CursorNs = Encoding.UTF8.GetString(buffer, offset, nsLen);
                        offset += nsLen;
                    }
                    else
                    {
                        msg.CursorNs = "";
                    }

                    if (offset < end)
                    {
                        int nameLen = buffer[offset];
                        offset += 1;

                        if (nameLen > 0 && offset + nameLen <= end)
                        {
                            msg.CursorName = Encoding.UTF8.GetString(buffer, offset, nameLen);
                            offset += nameLen;
                        }
                        else
                        {
                            msg.CursorName = "";
                        }
                    }

                    if (offset + 4 <= end)
                    {
                        msg.CursorCount = MessageSerializer.ReadUShort(buffer, offset);
                        offset += 2;
                        msg.CursorDurability = (short)MessageSerializer.ReadUShort(buffer, offset);
                    }
                }
            }

            return msg;
        }

        /// <summary>Returns the serialized byte size of a single slot.</summary>
        private static int GetSlotSize(ref SyncSlot slot)
        {
            int nsLen = string.IsNullOrEmpty(slot.Ns) ? 0 : Encoding.UTF8.GetByteCount(slot.Ns);
            int nameLen = string.IsNullOrEmpty(slot.Name) ? 0 : Encoding.UTF8.GetByteCount(slot.Name);
            int compCount = slot.Components?.Length ?? 0;
            int compSize = 0;

            for (int c = 0; c < compCount; c++)
            {
                compSize += 2 + 2 + (slot.Components?[c].Data?.Length ?? 0);
            }

            // SlotIndex(1) + NsLen(1) + Ns + NameLen(1) + Name + Count(2) + Durability(2) + CompCount(1) + comps
            return 1 + 1 + nsLen + 1 + nameLen + 2 + 2 + 1 + compSize;
        }

        /// <summary>Serializes a single slot into the buffer.</summary>
        private static int SerializeSlot(ref SyncSlot slot, byte[] buffer, int offset)
        {
            int start = offset;

            buffer[offset] = slot.SlotIndex;
            offset += 1;

            // Namespace
            if (string.IsNullOrEmpty(slot.Ns))
            {
                buffer[offset] = 0;
                offset += 1;
            }
            else
            {
                byte[] nsBytes = Encoding.UTF8.GetBytes(slot.Ns);
                buffer[offset] = (byte)nsBytes.Length;
                offset += 1;
                Array.Copy(nsBytes, 0, buffer, offset, nsBytes.Length);
                offset += nsBytes.Length;
            }

            // Name
            if (string.IsNullOrEmpty(slot.Name))
            {
                buffer[offset] = 0;
                offset += 1;
            }
            else
            {
                byte[] nameBytes = Encoding.UTF8.GetBytes(slot.Name);
                buffer[offset] = (byte)nameBytes.Length;
                offset += 1;
                Array.Copy(nameBytes, 0, buffer, offset, nameBytes.Length);
                offset += nameBytes.Length;
            }

            MessageSerializer.WriteUShort(buffer, offset, slot.Count);
            offset += 2;
            MessageSerializer.WriteUShort(buffer, offset, (ushort)slot.Durability);
            offset += 2;

            int compCount = slot.Components?.Length ?? 0;
            buffer[offset] = (byte)compCount;
            offset += 1;

            for (int c = 0; c < compCount; c++)
            {
                if (slot.Components == null)
                {
                    continue;
                }

                SyncComponent comp = slot.Components[c];
                MessageSerializer.WriteUShort(buffer, offset, comp.TypeId);
                offset += 2;
                int dataLen = comp.Data?.Length ?? 0;
                MessageSerializer.WriteUShort(buffer, offset, (ushort)dataLen);
                offset += 2;

                if (dataLen > 0)
                {
                    if (comp.Data != null)
                    {
                        Array.Copy(comp.Data, 0, buffer, offset, dataLen);
                    }

                    offset += dataLen;
                }
            }

            return offset - start;
        }

        /// <summary>Deserializes a single slot from the buffer.</summary>
        private static SyncSlot DeserializeSlot(byte[] buffer, ref int offset, int end)
        {
            SyncSlot slot = new();

            if (offset >= end)
            {
                return slot;
            }

            slot.SlotIndex = buffer[offset];
            offset += 1;

            // Namespace
            int nsLen = buffer[offset];
            offset += 1;

            if (nsLen > 0 && offset + nsLen <= end)
            {
                slot.Ns = Encoding.UTF8.GetString(buffer, offset, nsLen);
                offset += nsLen;
            }
            else
            {
                slot.Ns = "";
            }

            // Name
            int nameLen = buffer[offset];
            offset += 1;

            if (nameLen > 0 && offset + nameLen <= end)
            {
                slot.Name = Encoding.UTF8.GetString(buffer, offset, nameLen);
                offset += nameLen;
            }
            else
            {
                slot.Name = "";
            }

            slot.Count = MessageSerializer.ReadUShort(buffer, offset);
            offset += 2;
            slot.Durability = (short)MessageSerializer.ReadUShort(buffer, offset);
            offset += 2;

            int compCount = buffer[offset];
            offset += 1;

            if (compCount > 0)
            {
                slot.Components = new SyncComponent[compCount];

                for (int c = 0; c < compCount && offset + 4 <= end; c++)
                {
                    SyncComponent comp = new()
                    {
                        TypeId = MessageSerializer.ReadUShort(buffer, offset),
                    };
                    offset += 2;
                    int dataLen = MessageSerializer.ReadUShort(buffer, offset);
                    offset += 2;

                    if (dataLen > 0 && offset + dataLen <= end)
                    {
                        comp.Data = new byte[dataLen];
                        Array.Copy(buffer, offset, comp.Data, 0, dataLen);
                        offset += dataLen;
                    }

                    slot.Components[c] = comp;
                }
            }

            return slot;
        }
    }

    /// <summary>A single inventory slot in a sync message.</summary>
    public struct SyncSlot
    {
        /// <summary>The inventory slot index (0-35).</summary>
        public byte SlotIndex;

        /// <summary>ResourceId namespace of the item.</summary>
        public string Ns;

        /// <summary>ResourceId name of the item.</summary>
        public string Name;

        /// <summary>Stack count.</summary>
        public ushort Count;

        /// <summary>Durability value (-1 for no durability).</summary>
        public short Durability;

        /// <summary>Serialized data components.</summary>
        public SyncComponent[] Components;
    }

    /// <summary>A serialized data component in a sync slot.</summary>
    public struct SyncComponent
    {
        /// <summary>Component type identifier.</summary>
        public ushort TypeId;

        /// <summary>Serialized component binary data.</summary>
        public byte[] Data;
    }
}
