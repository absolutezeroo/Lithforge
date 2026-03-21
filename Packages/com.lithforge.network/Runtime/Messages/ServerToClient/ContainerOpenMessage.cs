using System;
using System.Text;

using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Server-to-Client container open with slot contents.
    ///     Wire format: [WindowId:1][EntityTypeIdLen:1][EntityTypeId:N]
    ///     [PositionX:4][PositionY:4][PositionZ:4]
    ///     [SlotCount:1][per-slot: SlotIndex:1, Count:2, NsLen:1, Ns:N, NameLen:1, Name:N, Durability:2]
    /// </summary>
    public struct ContainerOpenMessage : INetworkMessage
    {
        /// <summary>Minimum payload size: WindowId(1) + TypeIdLen(1) + Pos(12) + SlotCount(1).</summary>
        public const int MinSize = 15;

        /// <summary>Assigned window ID for this container session.</summary>
        public byte WindowId;

        /// <summary>Block entity type identifier (e.g. "chest", "furnace").</summary>
        public string EntityTypeId;

        /// <summary>World X coordinate of the block entity.</summary>
        public int PositionX;

        /// <summary>World Y coordinate of the block entity.</summary>
        public int PositionY;

        /// <summary>World Z coordinate of the block entity.</summary>
        public int PositionZ;

        /// <summary>Slot contents of the opened container.</summary>
        public SyncSlot[] Slots;

        /// <summary>Returns the MessageType for this message.</summary>
        public MessageType Type
        {
            get { return MessageType.ContainerOpen; }
        }

        /// <summary>Returns the total serialized payload size.</summary>
        public int GetSerializedSize()
        {
            int typeIdLen = string.IsNullOrEmpty(EntityTypeId)
                ? 0
                : Encoding.UTF8.GetByteCount(EntityTypeId);
            int size = MinSize + typeIdLen;
            int slotCount = Slots?.Length ?? 0;

            for (int i = 0; i < slotCount; i++)
            {
                size += GetSlotSize(ref Slots[i]);
            }

            return size;
        }

        /// <summary>Writes the message payload into the buffer.</summary>
        public int Serialize(byte[] buffer, int offset)
        {
            int start = offset;

            buffer[offset] = WindowId;
            offset += 1;

            offset += WriteString(buffer, offset, EntityTypeId);

            MessageSerializer.WriteInt(buffer, offset, PositionX);
            offset += 4;
            MessageSerializer.WriteInt(buffer, offset, PositionY);
            offset += 4;
            MessageSerializer.WriteInt(buffer, offset, PositionZ);
            offset += 4;

            int slotCount = Slots?.Length ?? 0;
            buffer[offset] = (byte)slotCount;
            offset += 1;

            for (int i = 0; i < slotCount; i++)
            {
                offset += SerializeSlot(ref Slots[i], buffer, offset);
            }

            return offset - start;
        }

        /// <summary>Reads the message from the buffer.</summary>
        public static ContainerOpenMessage Deserialize(byte[] buffer, int offset, int length)
        {
            ContainerOpenMessage msg = new();
            int end = offset + length;

            if (length < MinSize)
            {
                return msg;
            }

            msg.WindowId = buffer[offset];
            offset += 1;

            msg.EntityTypeId = ReadString(buffer, ref offset, end);

            if (offset + 13 > end)
            {
                return msg;
            }

            msg.PositionX = MessageSerializer.ReadInt(buffer, offset);
            offset += 4;
            msg.PositionY = MessageSerializer.ReadInt(buffer, offset);
            offset += 4;
            msg.PositionZ = MessageSerializer.ReadInt(buffer, offset);
            offset += 4;

            int slotCount = buffer[offset];
            offset += 1;

            msg.Slots = new SyncSlot[slotCount];

            for (int i = 0; i < slotCount && offset < end; i++)
            {
                msg.Slots[i] = DeserializeSlot(buffer, ref offset, end);
            }

            return msg;
        }

        /// <summary>Returns the serialized size of a single slot.</summary>
        private static int GetSlotSize(ref SyncSlot slot)
        {
            int nsLen = string.IsNullOrEmpty(slot.Ns) ? 0 : Encoding.UTF8.GetByteCount(slot.Ns);
            int nameLen = string.IsNullOrEmpty(slot.Name) ? 0 : Encoding.UTF8.GetByteCount(slot.Name);
            return 1 + 2 + 1 + nsLen + 1 + nameLen + 2;
        }

        /// <summary>Serializes a single slot into the buffer.</summary>
        private static int SerializeSlot(ref SyncSlot slot, byte[] buffer, int offset)
        {
            int start = offset;

            buffer[offset] = slot.SlotIndex;
            offset += 1;

            MessageSerializer.WriteUShort(buffer, offset, slot.Count);
            offset += 2;

            offset += WriteString(buffer, offset, slot.Ns);
            offset += WriteString(buffer, offset, slot.Name);

            MessageSerializer.WriteUShort(buffer, offset, (ushort)slot.Durability);
            offset += 2;

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

            if (offset + 2 <= end)
            {
                slot.Count = MessageSerializer.ReadUShort(buffer, offset);
                offset += 2;
            }

            slot.Ns = ReadString(buffer, ref offset, end);
            slot.Name = ReadString(buffer, ref offset, end);

            if (offset + 2 <= end)
            {
                slot.Durability = (short)MessageSerializer.ReadUShort(buffer, offset);
                offset += 2;
            }

            return slot;
        }

        /// <summary>Writes a length-prefixed UTF-8 string. Returns bytes written.</summary>
        private static int WriteString(byte[] buffer, int offset, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                buffer[offset] = 0;
                return 1;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(value);
            buffer[offset] = (byte)bytes.Length;
            Array.Copy(bytes, 0, buffer, offset + 1, bytes.Length);
            return 1 + bytes.Length;
        }

        /// <summary>Reads a length-prefixed UTF-8 string from the buffer.</summary>
        private static string ReadString(byte[] buffer, ref int offset, int end)
        {
            if (offset >= end)
            {
                return "";
            }

            int len = buffer[offset];
            offset += 1;

            if (len > 0 && offset + len <= end)
            {
                string result = Encoding.UTF8.GetString(buffer, offset, len);
                offset += len;
                return result;
            }

            return "";
        }
    }
}
