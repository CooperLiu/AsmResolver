﻿using AsmResolver.Net.Metadata;

namespace AsmResolver.Net.Cts
{
    public class NestedClass : MetadataMember<MetadataRow<uint, uint>>
    {
        private readonly LazyValue<TypeDefinition> _class;
        private readonly LazyValue<TypeDefinition> _enclosingClass;

        public NestedClass(TypeDefinition @class) 
            : base(null, new MetadataToken(MetadataTokenType.NestedClass))
        {
            _class = new LazyValue<TypeDefinition>(@class);
            _enclosingClass = new LazyValue<TypeDefinition>(default(TypeDefinition));
        }
        
        internal NestedClass(MetadataImage image, MetadataRow<uint, uint> row)
            : base(image, row.MetadataToken)
        {
            var typeTable = image.Header.GetStream<TableStream>().GetTable(MetadataTokenType.TypeDef);

            _class = new LazyValue<TypeDefinition>(() =>
            {
                MetadataRow type;
                return typeTable.TryGetRow((int) (row.Column1 - 1), out type)
                    ? (TypeDefinition) typeTable.GetMemberFromRow(image, type)
                    : null;
            });

            _enclosingClass = new LazyValue<TypeDefinition>(() =>
            {
                MetadataRow type;
                return typeTable.TryGetRow((int) (row.Column2 - 1), out type)
                    ? (TypeDefinition) typeTable.GetMemberFromRow(image, type)
                    : null;
            });

        }

        public TypeDefinition Class
        {
            get { return _class.Value; }
            set
            {
                if (_class.IsInitialized && _class.Value != null)
                    _class.Value.DeclaringType = null;
                _class.Value = value;
                if (value != null)
                    Class.DeclaringType = EnclosingClass;
            }
        }

        public TypeDefinition EnclosingClass
        {
            get { return _enclosingClass.Value; }
            internal set
            {
                _enclosingClass.Value = value;
                if (_class.IsInitialized && _class.Value != null)
                    _class.Value.DeclaringType = value;
            }
        }
    }
}