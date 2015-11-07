using System;

namespace GuitarSynthesizer.Helpers
{
    internal static class EnumHelper
    {
        public static T ConvertToEnum<T>(this string enumString)
        {
            try
            {
                return (T) Enum.Parse(typeof (T), enumString, true);
            }
            catch (Exception ex)
            {
                // Create an instance of T ... we're doing this to that we can perform a GetType() on it to retrieve the name
                //
                T temp = default(T);
                var s = $"'{enumString}' is not a valid enumeration of '{temp.GetType().Name}'";
                throw new Exception(s, ex);
            }
        }
    }
}