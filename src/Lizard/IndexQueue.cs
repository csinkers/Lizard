using System.Collections;

namespace Lizard;

public class IndexQueue<T> : IEnumerable<T>
{
	const int InitialCapacity = 8;
	const int ShrinkRatio = 3; // Will shrink when the Capacity:Count ratio exceeds this value

	T[] _array = new T[InitialCapacity]; // _array.Length = len, max item count before resize
	int _start; // [0..len)
	int _end;   // [0..2*len], when indices are >= len they refer to the items in _array[index - len]
	int _version;

	class IndexQueueEnumerator : IEnumerator<T>
	{
		readonly IndexQueue<T> _queue;
		readonly int _version;
		int _index = -1;

		public IndexQueueEnumerator(IndexQueue<T> queue)
		{
			_queue = queue;
			_version = _queue._version;
		}

		public T Current => _queue[_index]!;
		object IEnumerator.Current => Current!;
		public bool MoveNext()
		{
			if (_queue._version != _version)
				throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

			if (_index + 1 >= _queue.Count)
				return false;

			++_index;
			return true;
		}

		public void Reset() => _index = 0;
		public void Dispose() { }
	}

	public int Count => _end - _start;
	public void Clear()
	{
		_start = _end = 0;
		++_version;
	}

	public void Enqueue(T item)
	{
		var len = _array.Length;
		int limit = _start + len;

		if (_end >= limit)
		{
			Resize(_array.Length * 2);
			Enqueue(item);
			return;
		}

		int actualEnd = _end >= len ? _end - len : _end;
		_array[actualEnd] = item;
		++_version;
		++_end;
	}

	public T? Dequeue() =>
		!TryDequeue(out var item)
			? throw new InvalidOperationException($"Tried to dequeue from an empty {nameof(IndexQueue<T>)}")
			: item;

	public bool TryDequeue(out T? item)
	{
		if (Count == 0)
		{
			item = default(T);
			return false;
		}

		item = _array[_start];
		_start++;
		if (_start == _array.Length)
		{
			_start = 0;
			_end -= _array.Length;
		}

		int newCount = Count;
		if (_array.Length > InitialCapacity && _array.Length > newCount * ShrinkRatio)
			Resize(_array.Length / 2);

		++_version;
		return true;
	}

	public T? Peek()
	{
		if (Count == 0)
			throw new InvalidOperationException($"Tried to peek from an empty {nameof(IndexQueue<T>)}");

		return _array[_start];
	}

	public bool TryPeek(out T? item)
	{
		if (Count == 0)
		{
			item = default(T);
			return false;
		}

		item = _array[_start];
		return true;
	}

	public T? this[int index]
	{
		get
		{
			var rel = _start + index;
			if (index < 0)
				throw new ArgumentOutOfRangeException($"Tried to access negative element {index}");

			if (rel >= _end)
				throw new ArgumentOutOfRangeException($"Tried to access element {index}, but the queue only contains {Count} elements");

			if (rel < _array.Length)
				return _array[rel];

			return _array[rel - _array.Length];
		}
	}

	public IEnumerator<T> GetEnumerator() => new IndexQueueEnumerator(this);

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	void Resize(int newSize)
	{
		var count = Count;
		var newArray = new T[newSize];

		if (_end < _array.Length)
		{
			var span = _array.AsSpan(_start, _end - _start);
			span.CopyTo(newArray);
		}
		else
		{
			var excess = 1 + _end - _array.Length;
			var span1 = _array.AsSpan(_start);
			span1.CopyTo(newArray);

			var span2 = _array.AsSpan(0, excess);
			span2.CopyTo(newArray.AsSpan(span1.Length));
		}

		_array = newArray;
		_start = 0;
		_end = count;
	}
}
