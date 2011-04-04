using System;

namespace RiftShark
{
    public sealed class RiftPacketField
    {
        public readonly ERiftPacketFieldType Type;
        public readonly int Index;
        public RiftPacketFieldValue Value = null;

        public RiftPacketField(ERiftPacketFieldType pType, int pIndex)
        {
            Type = pType;
            Index = pIndex;
        }
    }
}
