using Lithforge.Network.Message;

namespace Lithforge.Network.Messages
{
    /// <summary>
    ///     Client→Server inventory slot click command. Mirrors the fields of
    ///     <see cref="Lithforge.Voxel.Command.SlotClickCommand"/> for wire transmission.
    ///     Wire format: [StateId:4][SequenceId:2][SlotIndex:2][ClickType:1][Button:1] = 10 bytes.
    /// </summary>
    public struct SlotClickCmdMessage : INetworkMessage
    {
        /// <summary>Fixed payload size in bytes.</summary>
        public const int Size = 10;

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

        /// <summary>Returns the MessageType for this message.</summary>
        public MessageType Type
        {
            get { return MessageType.SlotClickCmd; }
        }

        /// <summary>Returns the fixed payload size.</summary>
        public int GetSerializedSize()
        {
            return Size;
        }

        /// <summary>Writes the message payload into the buffer.</summary>
        public int Serialize(byte[] buffer, int offset)
        {
            MessageSerializer.WriteUInt(buffer, offset, StateId);
            offset += 4;
            MessageSerializer.WriteUShort(buffer, offset, SequenceId);
            offset += 2;
            MessageSerializer.WriteUShort(buffer, offset, (ushort)SlotIndex);
            offset += 2;
            buffer[offset] = ClickType;
            offset += 1;
            buffer[offset] = Button;
            return Size;
        }

        /// <summary>Reads the message from the buffer.</summary>
        public static SlotClickCmdMessage Deserialize(byte[] buffer, int offset, int length)
        {
            SlotClickCmdMessage msg = new();

            if (length < Size)
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
            return msg;
        }
    }
}
