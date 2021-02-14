﻿using System.Collections.Generic;
using AsmResolver.DotNet;
using AsmResolver.Workspaces.Dotnet.Analyzers;

namespace AsmResolver.Workspaces.Dotnet
{
    /// <summary>
    /// Represents a workspace of .NET assemblies.
    /// </summary>
    public class DotNetWorkspace : Workspace
    {
        /// <summary>
        /// Creates a new instance of the <see cref="DotNetWorkspace"/> class.
        /// </summary>
        public DotNetWorkspace()
        {
            Analyzers.Register(typeof(AssemblyDefinition), new AssemblyAnalyzer());
            Analyzers.Register(typeof(ModuleDefinition), new ModuleAnalyzer());
            Analyzers.Register(typeof(TypeDefinition), new TypeAnalyzer());
            Analyzers.Register(typeof(MethodDefinition), new MethodImplementationAnalyzer());
        }

        /// <summary>
        /// Gets a collection of assemblies added to the workspace.
        /// </summary>
        public IList<AssemblyDefinition> Assemblies
        {
            get;
        } = new List<AssemblyDefinition>();

        /// <summary>
        /// Analyzes all the assemblies in the workspace.
        /// </summary>
        public void Analyze()
        {
            var context = new AnalysisContext(this);

            for (int i = 0; i < Assemblies.Count; i++)
                context.SchedulaForAnalysis(Assemblies[i]);

            base.Analyze(context);
        }

    }
}
