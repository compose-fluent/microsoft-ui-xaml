// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.UI.Xaml.Markup.Compiler.XBF
{
#if KOTLIN_UWP_XAML
    // Windows SDK Microsoft.Windows.UI.Xaml.Build.Tasks uses its own private XBF ABI.
    [ComImport, Guid("1f2cce34-4bb6-478a-9426-d5f96f87588e"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
#else
    [ComImport, Guid("d0aa6fc8-087f-46cf-b36a-7e68f8295ceb"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
#endif
    internal interface IXbfMember
    {
        bool IsAttachable {[return: MarshalAs(UnmanagedType.U1)] get; }
        bool IsDependencyProperty {[return: MarshalAs(UnmanagedType.U1)] get; }
        bool IsReadOnly {[return: MarshalAs(UnmanagedType.U1)] get; }
        string Name {[return: MarshalAs(UnmanagedType.BStr)] get; }
        IXbfType TargetType {[return: MarshalAs(UnmanagedType.Interface)] get; }
        IXbfType Type {[return: MarshalAs(UnmanagedType.Interface)] get; }
        [return: MarshalAs(UnmanagedType.IInspectable)]
        object GetValue([MarshalAs(UnmanagedType.IInspectable)] object instance);
        void SetValue([MarshalAs(UnmanagedType.IInspectable)] object instance, [MarshalAs(UnmanagedType.IInspectable)] object value);
    }

#if KOTLIN_UWP_XAML
    [ComImport, Guid("176eb96d-efe2-49e4-8b5e-4ca5f03d3010"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
#else
    [ComImport, Guid("a50fc345-4c61-411b-8a68-13da7b7c4ee4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
#endif
    internal interface IXbfType
    {
        IXbfType BaseType {[return: MarshalAs(UnmanagedType.Interface)] get; }
        IXbfMember ContentProperty {[return: MarshalAs(UnmanagedType.Interface)] get; }
        string FullName {[return: MarshalAs(UnmanagedType.BStr)] get; }
        bool IsArray {[return: MarshalAs(UnmanagedType.U1)] get; }
        bool IsCollection {[return: MarshalAs(UnmanagedType.U1)] get; }
        bool IsConstructible {[return: MarshalAs(UnmanagedType.U1)] get; }
        bool IsDictionary {[return: MarshalAs(UnmanagedType.U1)] get; }
        bool IsMarkupExtension {[return: MarshalAs(UnmanagedType.U1)] get; }
        bool IsBindable {[return: MarshalAs(UnmanagedType.U1)] get; }
        IXbfType ItemType {[return: MarshalAs(UnmanagedType.Interface)] get; }
        IXbfType KeyType {[return: MarshalAs(UnmanagedType.Interface)] get; }
#if !KOTLIN_UWP_XAML
        IXbfType BoxedType {[return: MarshalAs(UnmanagedType.Interface)] get; }
#endif
        Type UnderlyingType { get; } // Type will probably not get correctly marshalled to TypeName in a classic COM interface definition. However, we shouldn't need this for XBF generation.
        [return: MarshalAs(UnmanagedType.IInspectable)]
        object ActivateInstance();
        [return: MarshalAs(UnmanagedType.IInspectable)]
        object CreateFromString([MarshalAs(UnmanagedType.BStr)] string value);
        [return: MarshalAs(UnmanagedType.Interface)]
        IXbfMember GetMember([MarshalAs(UnmanagedType.BStr)] string name);
        void AddToVector([MarshalAs(UnmanagedType.IInspectable)] object instance, [MarshalAs(UnmanagedType.IInspectable)] object value);
        void AddToMap([MarshalAs(UnmanagedType.IInspectable)] object instance, [MarshalAs(UnmanagedType.IInspectable)] object key, [MarshalAs(UnmanagedType.IInspectable)] object value);
        void RunInitializer();
    }

#if KOTLIN_UWP_XAML
    [ComImport, Guid("20cdd792-5406-4c84-af72-9efb375c8da8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
#else
    [ComImport, Guid("ef46679c-4ec5-447a-bd26-e04f1d2c2551"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
#endif
    internal interface IXbfMetadataProvider
    {
        IXbfType GetXamlType(Type type);
        IXbfType GetXamlType([MarshalAs(UnmanagedType.BStr)] string fullName);
        object[] GetXmlnsDefinitions();
    }
}
