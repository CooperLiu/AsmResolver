using System;
using System.Collections.Generic;

namespace AsmResolver.PE.Code
{
    /// <summary>
    /// Represents a chunk of native code in a portable executable.
    /// </summary>
    public class CodeSegment : SegmentBase
    {
        /// <summary>
        /// Creates a new segment of native code.
        /// </summary>
        /// <param name="imageBase">The base address of the image the segment is going to be stored in.</param>
        /// <param name="code">The raw native code stream.</param>
        public CodeSegment(ulong imageBase, byte[] code)
        {
            ImageBase = imageBase;
            Code = code ?? throw new ArgumentNullException(nameof(code));
        }

        /// <summary>
        /// Gets the base address of the image the segment is stored in.
        /// </summary>
        public ulong ImageBase
        {
            get;
        }

        /// <summary>
        /// Gets or sets the raw native code stream.
        /// </summary>
        public byte[] Code
        {
            get;
            set;
        }

        /// <summary>
        /// Gets a collection of fixups that need to be applied upon writing the code to the output stream.
        /// This includes addresses to imported symbols and global fields stored in data sections.
        /// </summary>
        public IList<AddressFixup> AddressFixups
        {
            get;
        } = new List<AddressFixup>();
        
        /// <inheritdoc />
        public override uint GetPhysicalSize() => (uint) Code.Length;

        /// <inheritdoc />
        public override void Write(IBinaryStreamWriter writer)
        {
            writer.WriteBytes(Code);

            for (int i = 0; i < AddressFixups.Count; i++)
                ApplyAddressFixup(writer, AddressFixups[i]);

            writer.Offset = Offset + GetPhysicalSize();
        }

        private void ApplyAddressFixup(IBinaryStreamWriter writer, in AddressFixup fixup)
        {
            writer.Offset = Offset + fixup.Offset;
            ulong rva = fixup.Offset - Offset + Rva;
            switch (fixup.Type)
            {
                case AddressFixupType.Absolute32BitAddress:
                    writer.WriteUInt32((uint) (ImageBase + fixup.Reference.Rva));
                    break;
                case AddressFixupType.Relative32BitAddress:
                    writer.WriteInt32((int) (fixup.Reference.Rva - (rva + 4)));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
    }
}