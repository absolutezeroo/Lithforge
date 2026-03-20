using System;
using System.Text;

using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Client-to-Server inventory slot click command with predicted outcomes.
    ///     Wire format: [StateId:4][SequenceId:2][SlotIndex:2][ClickType:1][Button:1]
    ///     [PredictedSlotCount:1][per-slot: SlotIndex:1, Count:2, NsLen:1, Ns:N, NameLen:1, Name:N, Durability:2]
    ///     [CursorCount:2][if > 0: CursorNsLen:1, CursorNs:N, CursorNameLen:1, CursorName:N, CursorDurability:2]
    /// </summary>
    public struct SlotClickCmdMessage : INetworkMessage
    {
        /// <summary>Minimum payload size: fixed header (10) + predicted count (1) + cursor count (2).</summary>
        public const int MinSize = 13;

        /// <summary>The client's last-known inventory state ID.</summary>
        public uint StateId;

        /// <summary>Per-player monotonic sequence number for ordering.</summary>
        public ushort SequenceId;

        /// <summary>Container slot index (-1 encoded as 0xFFFF for outside/drop).</summary>
        public short SlotIndex;

        /// <summary>Click type (0=Left, 1=Right, 2=ShiftLeft, 3=PaintDrag, 4=NumberKey, 5=OutputTake).</summary>
        public byte ClickType;

        /// <summary>Context byte: hotbar index for NumberKey, drag phase for PaintDrag.</summary>
        public byte Button;

        /// <summary>Client-predicted slot changes after the click. Null if no predictions.</summary>
        public PredictedSlot[] PredictedSlots;

        /// <summary>Client-predicted cursor namespace after the click. Empty if cursor is empty.</summary>
        public string CursorNs;

        /// <summary>Client-predicted cursor name after the click. Empty if cursor is empty.</summary>
        public string CursorName;

        /// <summary>Client-predicted cursor count after the click. 0 = empty.</summary>
        public ushort CursorCount;

        /// <summary>Client-predicted cursor durability after the click.</summary>
        public short CursorDurability;

        /// <summary>Returns the MessageType for this message.</summary>
        public MessageType Type
        {
            get { return MessageType.SlotClickCmd; }
        }

        /// <summary>Returns the total serialized payload size.</summary>
        public int GetSerializedSize()
        {
            int size = MinSize;
            int slotCount = PredictedSlots?.Length ?? 0;

            for (int i = 0; i < slotCount; i++)
            {
                size += GetPredictedSlotSize(ref PredictedSlots[i]);
            }

            if (CursorCount > 0)
            {
                int nsLen = string.IsNullOrEmpty(CursorNs) ? 0 : Encoding.UTF8.GetByteCount(CursorNs);
                int nameLen = string.IsNullOrEmpty(CursorName)
                    ? 0
                    : Encoding.UTF8.GetByteCount(CursorName);
                size += 1 + nsLen + 1 + nameLen + 2;
            }

            return size;
        }

        /// <summary>Writes the message payload into the buffer.</summary>
        public int Serialize(byte[] buffer, int offset)
        {
            int start = offset;

            MessageSerializer.WriteUInt(buffer, offset, StateId);
            offset += 4;
            MessageSerializer.WriteUShort(buffer, offset, SequenceId);
            offset += 2;
            MessageSerializer.WriteUShort(buffer, offset, (ushort)SlotIndex);
            offset += 2;
            buffer[offset] = ClickType;
            offset += 1;
            buffer[offset] = Button;
            offset += 1;

            int slotCount = PredictedSlots?.Length ?? 0;
            buffer[offset] = (byte)slotCount;
            offset += 1;

            for (int i = 0; i < slotCount; i++)
            {
                offset += SerializePredictedSlot(ref PredictedSlots[i], buffer, offset);
            }

            MessageSerializer.WriteUShort(buffer, offset, CursorCount);
            offset += 2;

            if (CursorCount > 0)
            {
                offset += WriteString(buffer, offset, CursorNs);
                offset += WriteString(buffer, offset, CursorName);
                MessageSerializer.WriteUShort(buffer, offset, (ushort)CursorDurability);
                offset += 2;
            }

            return offset - start;
        }

        /// <summary>Reads the message from the buffer.</summary>
        public static SlotClickCmdMessage Deserialize(byte[] buffer, int offset, int length)
        {
            SlotClickCmdMessage msg = new();
            int end = offset + length;

            if (length < 10)
            {
                return msg;
            }

            msg.StateId = MessageSerializer.ReadUInt(buffer, offset);
            offset += 4;
            msg.SequenceId = MessageSerializer.ReadUShort(buffer, offset);
            offset += 2;
            msg.SlotIndex = (short)MessageSerializer.ReadUShort(buffer, offset);
            offset += 2;
            msg.ClickType = buffer[offset];
            offset += 1;
            msg.Button = buffer[offset];
            offset += 1;

            // Predictions are optional for backward compatibility
            if (offset >= end)
            {
                return msg;
            }

            int slotCount = buffer[offset];
            offset += 1;

            if (slotCount > 0)
            {
                msg.PredictedSlots = new PredictedSlot[slotCount];

                for (int i = 0; i < slotCount && offset < end; i++)
                {
                    msg.PredictedSlots[i] = DeserializePredictedSlot(buffer, ref offset, end);
                }
            }

            if (offset + 2 <= end)
            {
                msg.CursorCount = MessageSerializer.ReadUShort(buffer, offset);
                offset += 2;

                if (msg.CursorCount > 0 && offset < end)
                {
                    msg.CursorNs = ReadString(buffer, ref offset, end);
                    msg.CursorName = ReadString(buffer, ref offset, end);

                    if (offset + 2 <= end)
                    {
                        msg.CursorDurability = (short)MessageSerializer.ReadUShort(buffer, offset);
                    }
                }
            }

            return msg;
        }

        /// <summary>Returns the serialized byte size of a single predicted slot.</summary>
        private static int GetPredictedSlotSize(ref PredictedSlot slot)
        {
            int nsLen = string.IsNullOrEmpty(slot.Ns) ? 0 : Encoding.UTF8.GetByteCount(slot.Ns);
            int nameLen = string.IsNullOrEmpty(slot.Name) ? 0 : Encoding.UTF8.GetByteCount(slot.Name);

            // SlotIndex(1) + Count(2) + NsLen(1) + Ns + NameLen(1) + Name + Durability(2)
            return 1 + 2 + 1 + nsLen + 1 + nameLen + 2;
        }

        /// <summary>Serializes a single predicted slot into the buffer.</summary>
        private static int SerializePredictedSlot(ref PredictedSlot slot, byte[] buffer, int offset)
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

        /// <summary>Deserializes a single predicted slot from the buffer.</summary>
        private static PredictedSlot DeserializePredictedSlot(byte[] buffer, ref int offset, int end)
        {
            PredictedSlot slot = new();

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

        /// <summary>Writes a length-prefixed UTF-8 string into the buffer. Returns bytes written.</summary>
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

    /// <summary>A client-predicted slot state after a click operation.</summary>
    public struct PredictedSlot
    {
        /// <summary>Inventory slot index (0-35).</summary>
        public byte SlotIndex;

        /// <summary>Predicted item namespace. Empty if slot predicted to be empty.</summary>
        public string Ns;

        /// <summary>Predicted item name. Empty if slot predicted to be empty.</summary>
        public string Name;

        /// <summary>Predicted count. 0 = predicted empty.</summary>
        public ushort Count;

        /// <summary>Predicted durability (-1 for no durability).</summary>
        public short Durability;
    }
}
