// -----------------------------------------------------------------------
// <copyright file="DefaultException.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. Licensed under the MIT License.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.WinGet.RestSource.Utils.Exceptions
{
    using System;
    using System.Net.Http;
    using Microsoft.WinGet.RestSource.Utils.Constants;
    using Microsoft.WinGet.RestSource.Utils.Models.Errors;

    /// <summary>
    /// This is the base exception for the WinGet.RestSource.
    /// </summary>
    public class DefaultException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultException"/> class.
        /// </summary>
        public DefaultException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultException"/> class.
        /// </summary>
        /// <param name="internalRestError">Internal Rest Error.</param>
        public DefaultException(InternalRestError internalRestError)
            : base(internalRestError.ErrorMessage)
        {
            this.InternalRestError = internalRestError;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultException"/> class.
        /// </summary>
        /// <param name="message">Exception message.</param>
        public DefaultException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultException"/> class.
        /// </summary>
        /// <param name="message">Exception message.</param>
        /// <param name="internalRestError">Internal Rest Error.</param>
        public DefaultException(string message, InternalRestError internalRestError)
            : base(message)
        {
            this.InternalRestError = internalRestError;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultException"/> class.
        /// </summary>
        /// <param name="message">Exception message.</param>
        /// <param name="inner">Inner exception.</param>
        public DefaultException(string message, Exception inner)
            : base(message, inner)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultException"/> class.
        /// </summary>
        /// <param name="message">Exception message.</param>
        /// <param name="inner">Inner exception.</param>
        /// <param name="internalRestError">Internal Rest Error.</param>
        public DefaultException(string message, Exception inner, InternalRestError internalRestError)
            : base(message, inner)
        {
            this.InternalRestError = internalRestError;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultException"/> class.
        /// </summary>
        /// <param name="exception">Exception.</param>
        public DefaultException(Exception exception)
            : base(exception.Message, exception.InnerException)
        {
            this.InternalRestError = exception switch
            {
                HttpRequestException => new InternalRestError(ErrorConstants.HttpRequestExceptionErrorCode, ErrorConstants.HttpRequestExceptionErrorMessage, exception),
                _ => new InternalRestError(ErrorConstants.UnhandledErrorCode, ErrorConstants.UnhandledErrorMessage, exception),
            };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultException"/> class.
        /// </summary>
        /// <param name="exception">Exception.</param>
        /// <param name="internalRestError">Internal Rest Error.</param>
        public DefaultException(Exception exception, InternalRestError internalRestError)
            : base(exception.Message, exception.InnerException)
        {
            this.InternalRestError = internalRestError;
        }

        /// <summary>
        /// Gets or sets internal error.
        /// </summary>
        public InternalRestError InternalRestError { get; set; }
    }
}
