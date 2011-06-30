﻿namespace AegisBornCommon
{
    public enum ErrorCode
    {
        /// <summary>
        /// The ok code.
        /// </summary>
        Ok = 0,

        /// <summary>
        /// The fatal.
        /// </summary>
        Fatal = 1,

        /// <summary>
        /// The parameter out of range.
        /// </summary>
        ParameterOutOfRange = 51,

        /// <summary>
        /// The operation not supported.
        /// </summary>
        OperationNotSupported,

        /// <summary>
        /// The invalid operation parameter.
        /// </summary>
        InvalidOperationParameter,

        /// <summary>
        /// The invalid operation.
        /// </summary>
        InvalidOperation, 

        /// <summary>
        /// The username or password isn't correct, don't let them log in.
        /// </summary>
        InvalidUserPass,

        /// <summary>
        /// Something happened with the character, look in the string.
        /// </summary>
        InvalidCharacter,
    }
}
