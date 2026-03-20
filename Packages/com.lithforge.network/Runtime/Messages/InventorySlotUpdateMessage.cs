using System;
using System.Text;

using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Server-to-Client targeted single-slot correction.
    ///     Wire format: [WindowId:1][StateId:4][SlotIndex:2][Count:2]
    ///     If Count > 0: [NsLen:1][Ns:N][NameLen:1][Name:N][Durability:2]
    ///     SlotIndex 0x7FFF = cursor slot. Count = 0 means empty slot.
    ///     Window 0 = player inventory.
    /// </summary>
    public struct InventorySlotUpdateMessage : INetworkMessage
    {
        /// <summary>Sentinel slot index indicating this update targets the cursor.</summary>
        public const short CursorSlotIndex = 0x7FFF;

        /// <summary>Minimum payload size: WindowId(1) + StateId(4) + SlotIndex(2) + Count(2).</summary>
        public const int MinSize = 9;

        /// <summary>Window identifier. 0 = player inventory.</summary>
        public byte WindowId;

        /// <summary>Server's inventory state ID at the time of this update.</summary>
        public uint StateId;

        /// <summary>Slot index to update. 0x7FFF = cursor slot.</summary>
        public short SlotIndex;

        /// <summary>Item namespace. Null or empty if slot is empty.</summary>
        public string Ns;

        /// <summary>Item name. Null or empty if slot is empty.</summary>
        public string Name;

        /// <summary>Stack count. 0 means empty.</summary>
        public ushort Count;

        /// <summary>Durability value (-1 for no durability).</summary>
        public short Durability;

        /// <summary>Returns the MessageType for this message.</summary>
        public MessageType Type
        {
            get { return MessageType.InventorySlotUpdate; }
        }

        /// <summary>Returns the total serialized payload size.</summary>
        public int GetSerializedSize()
        {
            int size = MinSize;

            if (Count > 0)
            {
                int nsLen = string.IsNullOrEmpty(Ns) ? 0 : Encoding.UTF8.GetByteCount(Ns);
                int nameLen = string.IsNullOrEmpty(Name) ? 0 : Encoding.UTF8.GetByteCount(Name);
                size += 1 + nsLen + 1 + nameLen + 2;
            }

            return size;
        }

        /// <summary>Writes the message payload into the buffer.</summary>
        public int Serialize(byte[] buffer, int offset)
        {
            int start = offset;

            buffer[offset] = WindowId;
            offset += 1;

            MessageSerializer.WriteUInt(buffer, offset, StateId);
            offset += 4;

            MessageSerializer.WriteUShort(buffer, offset, (ushort)SlotIndex);
            offset += 2;

            MessageSerializer.WriteUShort(buffer, offset, Count);
            offset += 2;

            if (Count > 0)
            {
                if (string.IsNullOrEmpty(Ns))
                {
                    buffer[offset] = 0;
                    offset += 1;
                }
                else
                {
                    byte[] nsBytes = Encoding.UTF8.GetBytes(Ns);
                    buffer[offset] = (byte)nsBytes.Length;
                    offset += 1;
                    Array.Copy(nsBytes, 0, buffer, offset, nsBytes.Length);
                    offset += nsBytes.Length;
                }

                if (string.IsNullOrEmpty(Name))
                {
                    buffer[offset] = 0;
                    offset += 1;
                }
                else
                {
                    byte[] nameBytes = Encoding.UTF8.GetBytes(Name);
                    buffer[offset] = (byte)nameBytes.Length;
                    offset += 1;
                    Array.Copy(nameBytes, 0, buffer, offset, nameBytes.Length);
                    offset += nameBytes.Length;
                }

                MessageSerializer.WriteUShort(buffer, offset, (ushort)Durability);
                offset += 2;
            }

            return offset - start;
        }

        /// <summary>Reads the message from the buffer.</summary>
        public static InventorySlotUpdateMessage Deserialize(byte[] buffer, int offset, int length)
        {
            InventorySlotUpdateMessage msg = new();
            int end = offset + length;

            if (length < MinSize)
            {
                return msg;
            }

            msg.WindowId = buffer[offset];
            offset += 1;

            msg.StateId = MessageSerializer.ReadUInt(buffer, offset);
            offset += 4;

            msg.SlotIndex = (short)MessageSerializer.ReadUShort(buffer, offset);
            offset += 2;

            msg.Count = MessageSerializer.ReadUShort(buffer, offset);
            offset += 2;

            if (msg.Count > 0 && offset < end)
            {
                int nsLen = buffer[offset];
                offset += 1;

                if (nsLen > 0 && offset + nsLen <= end)
                {
                    msg.Ns = Encoding.UTF8.GetString(buffer, offset, nsLen);
                    offset += nsLen;
                }
                else
                {
                    msg.Ns = "";
                }

                if (offset < end)
                {
                    int nameLen = buffer[offset];
                    offset += 1;

                    if (nameLen > 0 && offset + nameLen <= end)
                    {
                        msg.Name = Encoding.UTF8.GetString(buffer, offset, nameLen);
                        offset += nameLen;
                    }
                    else
                    {
                        msg.Name = "";
                    }
                }

                if (offset + 2 <= end)
                {
                    msg.Durability = (short)MessageSerializer.ReadUShort(buffer, offset);
                }
            }

            return msg;
        }
    }
}
