// AsmResolver - Executable file format inspection library 
// Copyright (C) 2016-2019 Washi
// 
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA

using System;
using System.Collections.Generic;
using System.Linq;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;

namespace AsmResolver.PE.DotNet.Metadata.Tables
{
    public class SerializedTableStream : TablesStream
    {
        private readonly IReadableSegment _contents;
        private readonly ulong _validMask;
        private readonly ulong _sortedMask;
        private readonly uint[] _rowCounts;
        private readonly TableLayout[] _layouts;
        private readonly IndexSize[] _indexSizes;
        private readonly uint _headerSize;

        public SerializedTableStream(byte[] rawData)
            : this(new DataSegment(rawData))
        {
        }

        public SerializedTableStream(IReadableSegment contents)
        {
            _contents = contents ?? throw new ArgumentNullException(nameof(contents));

            var reader = contents.CreateReader();
            Reserved = reader.ReadUInt32();
            MajorVersion = reader.ReadByte();
            MinorVersion = reader.ReadByte();
            Flags = (TablesStreamFlags) reader.ReadByte();
            Log2LargestRid = reader.ReadByte();
            _validMask = reader.ReadUInt64();
            _sortedMask = reader.ReadUInt64();

            _rowCounts = ReadRowCounts(reader);

            if (HasExtraData)
                ExtraData = reader.ReadUInt32();

            _headerSize = reader.FileOffset - reader.StartPosition;

            _indexSizes = InitializeIndexSizes();
            _layouts = InitializeTableLayouts();
        }

        public override bool CanRead => true;

        public override IBinaryStreamReader CreateReader()
        {
            return _contents.CreateReader();
        }

        protected bool HasTable(TableIndex table)
        {
            return ((_validMask >> (int) table) & 1) != 0;
        }

        protected bool IsSorted(TableIndex table)
        {
            return ((_sortedMask >> (int) table) & 1) != 0;
        }

        private uint[] ReadRowCounts(IBinaryStreamReader reader)
        {
            const TableIndex maxTableIndex = TableIndex.GenericParamConstraint;
            
            var result = new uint[(int) maxTableIndex + 1 ];
            for (TableIndex i = 0; i <= maxTableIndex; i++)
                result[(int) i] = HasTable(i) ? reader.ReadUInt32() : 0;

            return result;
        }

        private TableLayout[] InitializeTableLayouts()
        {
            var result = new[]
            {
                new TableLayout(
                    new ColumnLayout("Generation", ColumnType.UInt16),
                    new ColumnLayout("Name", ColumnType.String, StringIndexSize),
                    new ColumnLayout("Mvid", ColumnType.Guid, GuidIndexSize),
                    new ColumnLayout("EncId", ColumnType.Guid, GuidIndexSize),
                    new ColumnLayout("EncBaseId", ColumnType.Guid, GuidIndexSize)),
                new TableLayout(
                    new ColumnLayout("ResolutionScope", ColumnType.ResolutionScope,
                        GetIndexSize(ColumnType.ResolutionScope)),
                    new ColumnLayout("Name", ColumnType.String, StringIndexSize),
                    new ColumnLayout("Namespace", ColumnType.Guid, StringIndexSize)),
                new TableLayout(
                    new ColumnLayout("Flags", ColumnType.UInt32),
                    new ColumnLayout("Name", ColumnType.String, StringIndexSize),
                    new ColumnLayout("Namespace", ColumnType.String, StringIndexSize),
                    new ColumnLayout("Extends", ColumnType.TypeDefOrRef,
                        GetIndexSize(ColumnType.TypeDefOrRef)),
                    new ColumnLayout("FieldList", ColumnType.Field, GetIndexSize(ColumnType.Field)),
                    new ColumnLayout("MethodList", ColumnType.Method, GetIndexSize(ColumnType.Method))),
                new TableLayout(
                    new ColumnLayout("Field", ColumnType.Field, GetIndexSize(ColumnType.Field))),
                new TableLayout(
                    new ColumnLayout("Flags", ColumnType.UInt16),
                    new ColumnLayout("Name", ColumnType.String, StringIndexSize),
                    new ColumnLayout("Signature", ColumnType.Blob, BlobIndexSize)),
                new TableLayout(
                    new ColumnLayout("Method", ColumnType.Method, GetIndexSize(ColumnType.Method))),
                new TableLayout(
                    new ColumnLayout("RVA", ColumnType.UInt32),
                    new ColumnLayout("ImplFlags", ColumnType.UInt16),
                    new ColumnLayout("Flags", ColumnType.UInt16),
                    new ColumnLayout("Name", ColumnType.String, StringIndexSize),
                    new ColumnLayout("Signature", ColumnType.Blob, BlobIndexSize),
                    new ColumnLayout("ParamList", ColumnType.Param, GetIndexSize(ColumnType.Param))),
                new TableLayout(
                    new ColumnLayout("Parameter", ColumnType.Param, GetIndexSize(ColumnType.Param))),
                new TableLayout(
                    new ColumnLayout("Flags", ColumnType.UInt16),
                    new ColumnLayout("Sequence", ColumnType.UInt16),
                    new ColumnLayout("Name", ColumnType.String, StringIndexSize)),
                new TableLayout(
                    new ColumnLayout("Class", ColumnType.TypeDef, GetIndexSize(ColumnType.TypeDef)),
                    new ColumnLayout("Interface", ColumnType.TypeDefOrRef, GetIndexSize(ColumnType.TypeDefOrRef))),
                new TableLayout(
                    new ColumnLayout("Parent", ColumnType.MemberRefParent, GetIndexSize(ColumnType.MemberRefParent)),
                    new ColumnLayout("Name", ColumnType.String, StringIndexSize),
                    new ColumnLayout("Signature", ColumnType.Blob, BlobIndexSize)),
                new TableLayout(
                    new ColumnLayout("Type", ColumnType.Byte),
                    new ColumnLayout("Padding", ColumnType.Byte),
                    new ColumnLayout("Parent", ColumnType.HasConstant, GetIndexSize(ColumnType.HasConstant)),
                    new ColumnLayout("Value", ColumnType.Blob, BlobIndexSize)), 
                new TableLayout(
                    new ColumnLayout("Parent", ColumnType.HasCustomAttribute, GetIndexSize(ColumnType.HasCustomAttribute)),
                    new ColumnLayout("Type", ColumnType.CustomAttributeType, GetIndexSize(ColumnType.CustomAttributeType)),
                    new ColumnLayout("Value", ColumnType.Blob, BlobIndexSize)),
                new TableLayout(
                    new ColumnLayout("Parent", ColumnType.HasFieldMarshal, GetIndexSize(ColumnType.HasFieldMarshal)),
                    new ColumnLayout("NativeType", ColumnType.Blob, BlobIndexSize)),
                new TableLayout(
                    new ColumnLayout("Action", ColumnType.UInt16),
                    new ColumnLayout("Parent", ColumnType.HasDeclSecurity, GetIndexSize(ColumnType.HasDeclSecurity)),
                    new ColumnLayout("PermissionSet", ColumnType.Blob, BlobIndexSize)),
                new TableLayout(
                    new ColumnLayout("PackingSize", ColumnType.UInt16),
                    new ColumnLayout("ClassSize", ColumnType.UInt32),
                    new ColumnLayout("Parent", ColumnType.TypeDef, GetIndexSize(ColumnType.TypeDef))),
                new TableLayout(
                    new ColumnLayout("Offset", ColumnType.UInt32),
                    new ColumnLayout("Field", ColumnType.TypeDef, GetIndexSize(ColumnType.Field))),
                new TableLayout(
                    new ColumnLayout("Signature", ColumnType.Blob, BlobIndexSize)),
                new TableLayout(
                    new ColumnLayout("Parent", ColumnType.TypeDef, GetIndexSize(ColumnType.TypeDef)),
                    new ColumnLayout("EventList", ColumnType.Event, GetIndexSize(ColumnType.Event))),
                new TableLayout(
                    new ColumnLayout("Event", ColumnType.Event, GetIndexSize(ColumnType.Event))),
                new TableLayout(
                    new ColumnLayout("Flags", ColumnType.UInt16),
                    new ColumnLayout("Name", ColumnType.String, StringIndexSize),
                    new ColumnLayout("EventType", ColumnType.TypeDefOrRef, GetIndexSize(ColumnType.TypeDefOrRef))),
                new TableLayout(
                    new ColumnLayout("Parent", ColumnType.TypeDef, GetIndexSize(ColumnType.TypeDef)),
                    new ColumnLayout("PropertyList", ColumnType.Event, GetIndexSize(ColumnType.Property))),
                new TableLayout(
                    new ColumnLayout("Property", ColumnType.Property, GetIndexSize(ColumnType.Property))),
                new TableLayout(
                    new ColumnLayout("Flags", ColumnType.UInt16),
                    new ColumnLayout("Name", ColumnType.String, StringIndexSize),
                    new ColumnLayout("PropertyType", ColumnType.Blob, BlobIndexSize)),
                new TableLayout(
                    new ColumnLayout("Semantic", ColumnType.UInt16),
                    new ColumnLayout("Method", ColumnType.Method, GetIndexSize(ColumnType.Method)),
                    new ColumnLayout("Association", ColumnType.HasSemantics, GetIndexSize(ColumnType.HasSemantics))),
                new TableLayout(
                    new ColumnLayout("Class", ColumnType.TypeDef, GetIndexSize(ColumnType.TypeDef)),
                    new ColumnLayout("MethodBody", ColumnType.MethodDefOrRef, GetIndexSize(ColumnType.MethodDefOrRef)),
                    new ColumnLayout("MethodDeclaration", ColumnType.MethodDefOrRef, GetIndexSize(ColumnType.MethodDefOrRef))),
                new TableLayout(
                    new ColumnLayout("Name", ColumnType.String, StringIndexSize)),
                new TableLayout(
                    new ColumnLayout("Signature", ColumnType.Blob, BlobIndexSize)),
                new TableLayout(
                    new ColumnLayout("MappingFlags", ColumnType.UInt16),
                    new ColumnLayout("MemberForwarded", ColumnType.MemberForwarded, GetIndexSize(ColumnType.MemberForwarded)),
                    new ColumnLayout("ImportName", ColumnType.String, StringIndexSize),
                    new ColumnLayout("ImportScope", ColumnType.ModuleRef, GetIndexSize(ColumnType.ModuleRef))),
                new TableLayout(
                    new ColumnLayout("RVA", ColumnType.UInt32),
                    new ColumnLayout("Field", ColumnType.Field, GetIndexSize(ColumnType.Field))),
                new TableLayout(
                    new ColumnLayout("Token", ColumnType.UInt32),
                    new ColumnLayout("FuncCode", ColumnType.UInt32)),
                new TableLayout(
                    new ColumnLayout("Token", ColumnType.UInt32)),
                new TableLayout(
                    new ColumnLayout("HashAlgId", ColumnType.UInt32),
                    new ColumnLayout("MajorVersion", ColumnType.UInt16),
                    new ColumnLayout("MinorVersion", ColumnType.UInt16),
                    new ColumnLayout("BuildNumber", ColumnType.UInt16),
                    new ColumnLayout("RevisionNumber", ColumnType.UInt16),
                    new ColumnLayout("Flags", ColumnType.UInt32),
                    new ColumnLayout("PublicKey", ColumnType.Blob, BlobIndexSize),
                    new ColumnLayout("Name", ColumnType.String, StringIndexSize),
                    new ColumnLayout("Culture", ColumnType.String, StringIndexSize)),
                new TableLayout(
                    new ColumnLayout("Processor", ColumnType.UInt32)),
                new TableLayout(
                    new ColumnLayout("PlatformId", ColumnType.UInt32),
                    new ColumnLayout("MajorVersion", ColumnType.UInt32),
                    new ColumnLayout("MinorVersion", ColumnType.UInt32)),
                new TableLayout(
                    new ColumnLayout("MajorVersion", ColumnType.UInt16),
                    new ColumnLayout("MinorVersion", ColumnType.UInt16),
                    new ColumnLayout("BuildNumber", ColumnType.UInt16),
                    new ColumnLayout("RevisionNumber", ColumnType.UInt16),
                    new ColumnLayout("Flags", ColumnType.UInt32),
                    new ColumnLayout("PublicKeyOrToken", ColumnType.Blob, BlobIndexSize),
                    new ColumnLayout("Name", ColumnType.String, StringIndexSize),
                    new ColumnLayout("Culture", ColumnType.String, StringIndexSize),
                    new ColumnLayout("HashValue", ColumnType.Blob, BlobIndexSize)),
                new TableLayout(
                    new ColumnLayout("Processor", ColumnType.UInt32),
                    new ColumnLayout("AssemblyRef", ColumnType.AssemblyRef, GetIndexSize(ColumnType.AssemblyRef))),
                new TableLayout(
                    new ColumnLayout("PlatformId", ColumnType.UInt32),
                    new ColumnLayout("MajorVersion", ColumnType.UInt32),
                    new ColumnLayout("MinorVersion", ColumnType.UInt32),
                    new ColumnLayout("AssemblyRef", ColumnType.AssemblyRef, GetIndexSize(ColumnType.AssemblyRef))),
                new TableLayout(
                    new ColumnLayout("Flags", ColumnType.UInt32),
                    new ColumnLayout("Name", ColumnType.String, StringIndexSize),
                    new ColumnLayout("HashValue", ColumnType.Blob, BlobIndexSize)),
                new TableLayout(
                    new ColumnLayout("Flags", ColumnType.UInt32),
                    new ColumnLayout("TypeDefId", ColumnType.UInt32),
                    new ColumnLayout("Name", ColumnType.String, StringIndexSize),
                    new ColumnLayout("Namespace", ColumnType.String, StringIndexSize),
                    new ColumnLayout("Implementation", ColumnType.Implementation, GetIndexSize(ColumnType.Implementation))),
                new TableLayout(
                    new ColumnLayout("Offset", ColumnType.UInt32),
                    new ColumnLayout("Flags", ColumnType.UInt32),
                    new ColumnLayout("Name", ColumnType.String, StringIndexSize),
                    new ColumnLayout("Implementation", ColumnType.Implementation, GetIndexSize(ColumnType.Implementation))),
                new TableLayout(
                    new ColumnLayout("NestedClass", ColumnType.TypeDef, GetIndexSize(ColumnType.TypeDef)),
                    new ColumnLayout("EnclosingClass", ColumnType.TypeDef, GetIndexSize(ColumnType.TypeDef))),
                new TableLayout(
                    new ColumnLayout("Number", ColumnType.UInt16),
                    new ColumnLayout("Flags", ColumnType.UInt16),
                    new ColumnLayout("Owner", ColumnType.TypeOrMethodDef, GetIndexSize(ColumnType.TypeOrMethodDef)),
                    new ColumnLayout("EnclosingClass", ColumnType.String, StringIndexSize)),
                new TableLayout(
                    new ColumnLayout("Method", ColumnType.Method, GetIndexSize(ColumnType.Method)),
                    new ColumnLayout("Instantiation", ColumnType.Blob, BlobIndexSize)),
                new TableLayout(
                    new ColumnLayout("Owner", ColumnType.GenericParam, GetIndexSize(ColumnType.GenericParam)),
                    new ColumnLayout("Constraint", ColumnType.TypeDefOrRef, GetIndexSize(ColumnType.TypeDefOrRef))),
            };
            
            return result;
        }

        private IndexSize GetIndexSize(ColumnType columnType)
        {
            return _indexSizes[(int) columnType];
        }

        private IndexSize[] InitializeIndexSizes()
        {
            const ColumnType maxColumnType = ColumnType.String;

            var result = new List<IndexSize>((int) maxColumnType);

            // Add index sizes for each table:
            foreach (uint t in _rowCounts)
                result.Add(t > 0xFFFF ? IndexSize.Long : IndexSize.Short);

            // Add index sizes for each coded index:
            result.AddRange(new[]
            {
                // TypeDefOrRef
                GetCodedIndexSize(TableIndex.TypeDef, TableIndex.TypeRef, TableIndex.TypeSpec),

                // HasConstant
                GetCodedIndexSize(TableIndex.Field, TableIndex.Param, TableIndex.Property),

                // HasCustomAttribute
                GetCodedIndexSize(
                    TableIndex.Method, TableIndex.Field, TableIndex.TypeRef, TableIndex.TypeDef,
                    TableIndex.Param, TableIndex.InterfaceImpl, TableIndex.MemberRef, TableIndex.Module,
                    TableIndex.DeclSecurity, TableIndex.Property, TableIndex.Event, TableIndex.StandAloneSig,
                    TableIndex.ModuleRef, TableIndex.TypeSpec, TableIndex.Assembly, TableIndex.AssemblyRef,
                    TableIndex.File, TableIndex.ExportedType, TableIndex.ManifestResource, TableIndex.GenericParam,
                    TableIndex.GenericParamConstraint, TableIndex.MethodSpec),

                // HasFieldMarshal
                GetCodedIndexSize(TableIndex.Field, TableIndex.Param),

                // HasDeclSecurity
                GetCodedIndexSize(TableIndex.TypeDef, TableIndex.Method, TableIndex.Assembly),

                // MemberRefParent
                GetCodedIndexSize(
                    TableIndex.TypeDef, TableIndex.TypeRef, TableIndex.ModuleRef,
                    TableIndex.Method, TableIndex.TypeSpec),

                // HasSemantics
                GetCodedIndexSize(TableIndex.Event, TableIndex.Property),

                // MethodDefOrRef
                GetCodedIndexSize(TableIndex.Method, TableIndex.MemberRef),

                // MemberForwarded
                GetCodedIndexSize(TableIndex.Field, TableIndex.Method),

                // Implementation
                GetCodedIndexSize(TableIndex.File, TableIndex.AssemblyRef, TableIndex.ExportedType),

                // CustomAttributeType
                GetCodedIndexSize(0, 0, TableIndex.Method, TableIndex.MemberRef, 0),

                // ResolutionScope
                GetCodedIndexSize(TableIndex.Module, TableIndex.ModuleRef, TableIndex.AssemblyRef, TableIndex.TypeRef),
                
                // TypeOrMethodDef
                GetCodedIndexSize(TableIndex.TypeDef, TableIndex.Method),
            });

            return result.ToArray();
        }

        private IndexSize GetCodedIndexSize(params TableIndex[] tables)
        {
            int tableIndexBitCount = (int) Math.Ceiling(Math.Log(tables.Length, 2));
            int maxSmallTableMemberCount = ushort.MaxValue >> tableIndexBitCount;

            return tables.Select(t => _rowCounts[(int) t]).All(c => c < maxSmallTableMemberCount)
                ? IndexSize.Short
                : IndexSize.Long;
        }

        protected override IList<IMetadataTable> GetTables()
        {
            uint offset = _contents.FileOffset + _headerSize;
            return new IMetadataTable[]
            {
                CreateNextTable(TableIndex.Module, ref offset, ModuleDefinitionRow.FromReader),
                CreateNextTable(TableIndex.TypeRef, ref offset, TypeReferenceRow.FromReader),
                CreateNextTable(TableIndex.TypeDef, ref offset, TypeDefinitionRow.FromReader),
                CreateNextTable(TableIndex.FieldPtr, ref offset, FieldPointerRow.FromReader),
                CreateNextTable(TableIndex.Field, ref offset, FieldDefinitionRow.FromReader),
                CreateNextTable(TableIndex.MethodPtr, ref offset, MethodPointerRow.FromReader),
                CreateNextTable(TableIndex.Method, ref offset, MethodDefinitionRow.FromReader),
                CreateNextTable(TableIndex.ParamPtr, ref offset, ParameterPointerRow.FromReader),
                CreateNextTable(TableIndex.Param, ref offset, ParameterDefinitionRow.FromReader),
                CreateNextTable(TableIndex.InterfaceImpl, ref offset, InterfaceImplementationRow.FromReader),
                CreateNextTable(TableIndex.MemberRef, ref offset, MemberReferenceRow.FromReader),
                CreateNextTable(TableIndex.Constant, ref offset, ConstantRow.FromReader),
                CreateNextTable(TableIndex.CustomAttribute, ref offset, CustomAttributeRow.FromReader),
                CreateNextTable(TableIndex.FieldMarshal, ref offset, FieldMarshalRow.FromReader),
                CreateNextTable(TableIndex.DeclSecurity, ref offset, SecurityDeclarationRow.FromReader),
                CreateNextTable(TableIndex.ClassLayout, ref offset, ClassLayoutRow.FromReader),
                CreateNextTable(TableIndex.FieldLayout, ref offset, FieldLayoutRow.FromReader),
                CreateNextTable(TableIndex.StandAloneSig, ref offset, StandAloneSignatureRow.FromReader),
                CreateNextTable(TableIndex.EventMap, ref offset, EventMapRow.FromReader),
                CreateNextTable(TableIndex.EventPtr, ref offset, EventPointerRow.FromReader),
                CreateNextTable(TableIndex.Event, ref offset, EventDefinitionRow.FromReader),
                CreateNextTable(TableIndex.PropertyMap, ref offset, PropertyMapRow.FromReader),
                CreateNextTable(TableIndex.PropertyPtr, ref offset, PropertyPointerRow.FromReader),
                CreateNextTable(TableIndex.Property, ref offset, PropertyDefinitionRow.FromReader),
                CreateNextTable(TableIndex.MethodSemantics, ref offset, MethodSemanticsRow.FromReader),
                CreateNextTable(TableIndex.MethodImpl, ref offset, MethodImplementationRow.FromReader),
                CreateNextTable(TableIndex.ModuleRef, ref offset, ModuleReferenceRow.FromReader),
                CreateNextTable(TableIndex.TypeSpec, ref offset, TypeSpecificationRow.FromReader),
                CreateNextTable(TableIndex.ImplMap, ref offset, ImplementationMapRow.FromReader),
                CreateNextTable(TableIndex.FieldRva, ref offset, FieldRvaRow.FromReader),
                CreateNextTable(TableIndex.EncLog, ref offset, EncLogRow.FromReader),
                CreateNextTable(TableIndex.EncMap, ref offset, EncMapRow.FromReader),
                CreateNextTable(TableIndex.Assembly, ref offset, AssemblyDefinitionRow.FromReader),
                CreateNextTable(TableIndex.AssemblyProcessor, ref offset, AssemblyProcessorRow.FromReader),
                CreateNextTable(TableIndex.AssemblyOS, ref offset, AssemblyOSRow.FromReader),
                CreateNextTable(TableIndex.AssemblyRef, ref offset, AssemblyReferenceRow.FromReader),
                CreateNextTable(TableIndex.AssemblyRefProcessor, ref offset, AssemblyRefProcessorRow.FromReader),
                CreateNextTable(TableIndex.AssemblyRefOS, ref offset, AssemblyRefOSRow.FromReader),
                CreateNextTable(TableIndex.File, ref offset, FileReferenceRow.FromReader),
                CreateNextTable(TableIndex.ExportedType, ref offset, ExportedTypeRow.FromReader),
                CreateNextTable(TableIndex.ManifestResource, ref offset, ManifestResourceRow.FromReader),
                CreateNextTable(TableIndex.NestedClass, ref offset, NestedClassRow.FromReader),
                CreateNextTable(TableIndex.GenericParam, ref offset, GenericParameterRow.FromReader),
                CreateNextTable(TableIndex.MethodSpec, ref offset, MethodSpecificationRow.FromReader),
                CreateNextTable(TableIndex.GenericParamConstraint, ref offset, GenericParameterConstraintRow.FromReader),
            };
        }

        private IBinaryStreamReader CreateNextRawTableReader(TableIndex currentIndex, ref uint currentOffset)
        {
            int index = (int) currentIndex;
            uint rawSize = _layouts[index].RowSize * _rowCounts[index];
            var tableReader = _contents.CreateReader(currentOffset, rawSize);
            currentOffset += rawSize;
            return tableReader;
        }

        private SerializedMetadataTable<TRow> CreateNextTable<TRow>(
            TableIndex index,
            ref uint offset,
            SerializedMetadataTable<TRow>.ReadRowDelegate readRow)
            where TRow : struct, IMetadataRow
        {
            return new SerializedMetadataTable<TRow>(
                CreateNextRawTableReader(index, ref offset), _layouts[(int) index], readRow);
        }
        
    }
}