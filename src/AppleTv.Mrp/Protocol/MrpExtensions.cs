// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Reflection;

using Google.Protobuf;

using AppleTvControlLibrary.Mrp.Protobuf;

namespace AppleTvControlLibrary.Mrp.Protocol;

/// <summary>
/// A shared <see cref="ExtensionRegistry"/> covering every <c>ProtocolMessage</c> extension field
/// generated from the vendored pyatv <c>.proto</c> files, so a <see cref="ProtocolMessage"/> can be
/// parsed from the wire without silently dropping its typed payload.
/// </summary>
/// <remarks>
/// The Companion Link protobuf equivalent does not exist because Companion uses OPACK, not
/// protobuf; MRP messages, however, are protobuf extension fields on <c>ProtocolMessage</c>
/// (see e.g. <c>CryptoPairingMessage.proto</c>: <c>extend ProtocolMessage { optional
/// CryptoPairingMessage cryptoPairingMessage = 39; }</c>), and Google.Protobuf only decodes
/// extension fields that are registered up front via an <see cref="ExtensionRegistry"/> passed to
/// the parser.
/// </remarks>
public static class MrpExtensions
	{
	/// <summary>Gets a registry containing every generated <c>ProtocolMessage</c> extension field.</summary>
	public static ExtensionRegistry Registry
		{
		get;
		} = BuildRegistry ();

	private static ExtensionRegistry BuildRegistry ()
		{
		var registry = new ExtensionRegistry ();

		foreach (Type type in typeof (ProtocolMessage).Assembly.GetTypes ())
			{
			if (!type.IsClass || !type.IsAbstract || !type.IsSealed || !type.Name.EndsWith ("Extensions", StringComparison.Ordinal))
				{
				continue;
				}

			foreach (FieldInfo field in type.GetFields (BindingFlags.Public | BindingFlags.Static))
				{
				object? value = field.GetValue (null);
				if (value is not null)
					{
					TryAddExtension (registry, value);
					}
				}
			}

		return registry;
		}

	private static bool TryAddExtension (ExtensionRegistry registry, object value)
		{
		MethodInfo? addMethod = typeof (ExtensionRegistry).GetMethod ("Add", [value.GetType ()]);
		if (addMethod is null)
			{
			return false;
			}

		addMethod.Invoke (registry, [value]);
		return true;
		}
	}
