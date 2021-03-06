﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vertx
{
	/// <summary>
	/// This class exists to give the PropertyDrawer a class to bind to.
	/// </summary>
	[Serializable]
	public abstract class EnumToValueBase
	{
		[SerializeField] protected bool hidesFirstEnum;
	}

	public class HideFirstEnumValue : Attribute { }

	[Serializable]
	public class EnumToValue<T, TValue> : EnumToValueBase, IEnumerable<(T key, TValue value)>
		where T : Enum
	{
		[SerializeField] protected TValue[] values = null;

		public TValue GetValue(T key) => values[(int) (object) key];
		public TValue this[T key] => values[(int) (object) key];

		public int Count => values?.Length ?? 0;

		private Array valuesArray = null;

		public int IndexOf(TValue value)
		{
			for (int i = hidesFirstEnum ? 1 : 0; i < values.Length; i++)
			{
				if (value.Equals(values[i]))
					return i;
			}

			return -1;
		}

		public IEnumerator<(T key, TValue value)> GetEnumerator()
		{
			if (values == null) yield break;
			Array array = valuesArray ?? Enum.GetValues(typeof(T));
			for (int i = hidesFirstEnum ? 1 : 0; i < values.Length; i++)
				yield return ((T) array.GetValue(i), values[i]);
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	[Serializable]
	public class EnumToValueDictionary<T, TValue> :
		EnumToValueBase, IEnumerable<(T key, TValue value)>,
		ISerializationCallbackReceiver
		where T : Enum
	{
		[SerializeField] protected T[] keys = null;
		[SerializeField] protected TValue[] values = null;

		private Dictionary<T, TValue> dictionary;

		public TValue GetValue(T key) => dictionary[key];
		public TValue this[T key]
		{
			get
			{
				if (dictionary.TryGetValue(key, out var value))
					return value;
				return default;
			}
		}

		public bool TryGetValue(T key, out TValue value) => dictionary.TryGetValue(key, out value);

		private int length;

		public IEnumerator<(T key, TValue value)> GetEnumerator()
		{
			foreach (KeyValuePair<T, TValue> keyValuePair in dictionary)
				yield return (keyValuePair.Key, keyValuePair.Value);
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


		public void OnBeforeSerialize()
		{
			//Don't do anything because we directly modify and re-serialize this via EnumToValueDrawer.
		}

		public void OnAfterDeserialize()
		{
			int count = Mathf.Min(keys.Length, values.Length);
			dictionary = new Dictionary<T, TValue>();
			for (int i = hidesFirstEnum ? 1 : 0; i < count; i++)
			{
				T key = keys[i];
				if (dictionary.ContainsKey(key)) continue;
				dictionary.Add(key, values[i]);
			}
		}
	}
}