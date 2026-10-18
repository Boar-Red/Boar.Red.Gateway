namespace Boar.Red.Gateway.Extensions
{
	public static class CharExtension
	{
		public static bool EqualsIgnoreCase(this char currentChar, char anotherChar)
		{
			if (currentChar == anotherChar)
			{
				return true;
			}
			else if (currentChar >= 'a'
			    && currentChar <= 'z')
			{
				if (anotherChar >= 'A'
				    && anotherChar <= 'Z')
				{
					return true;
				}
			}
			else if (currentChar >= 'A'
			    && currentChar <= 'Z')
			{
				if (anotherChar >= 'a'
				    && anotherChar <= 'z')
				{
					return true;
				}
			}
			return false;
		}

		public static bool IsCharsOfStringEqualsKey(
		    string key,
		    string str,
		    int beginCharIndex,
		    bool isIgnoreCase = true)
		{
			if (str == null
			    || str.Length < 1)
			{
				return false;
			}
			if (key == null
			    || key.Length < 1)
			{
				return false;
			}
			if (beginCharIndex + key.Length > str.Length)
			{
				return false;
			}

			if (isIgnoreCase)
			{
				for (var charIndex = 0;
				    charIndex < key.Length;
				    charIndex++)
				{
					var strChar = str[beginCharIndex + charIndex];
					var keyChar = key[charIndex];
					if (strChar.EqualsIgnoreCase(keyChar) != true)
					{
						return false;
					}
				}
			}
			else
			{
				for (var charIndex = 0;
				    charIndex < key.Length;
				    charIndex++)
				{
					var strChar = str[beginCharIndex + charIndex];
					var keyChar = key[charIndex];
					if (strChar != keyChar)
					{
						return false;
					}
				}
			}
			return true;
		}
	}
}
