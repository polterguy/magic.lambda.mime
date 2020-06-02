﻿/*
 * Magic, Copyright(c) Thomas Hansen 2019 - 2020, thomas@servergardens.com, all rights reserved.
 * See the enclosed LICENSE file for details.
 */

using System.Linq;
using System.Security;
using MimeKit;
using MimeKit.Cryptography;
using magic.node;

namespace magic.lambda.mime.helpers
{
    /// <summary>
    /// Helper class to parse MIME messages.
    /// </summary>
    public static class MimeParser
    {
        /// <summary>
        /// Parses a MimeEntity and returns as lambda to caller.
        /// </summary>
        /// <param name="node">Node containing the MIME message as value, and also where the lambda structure representing the parsed message will be placed.</param>
        /// <param name="entity"> MimeEntity to parse.</param>
        public static void Parse(Node node, MimeEntity entity)
        {
            var tmp = new Node("entity", entity.ContentType.MimeType);
            ProcessHeaders(tmp, entity);

            if (entity is MultipartSigned signed)
            {
                // Multipart content.
                var signatures = new Node("signatures");
                using (var ctx = new PGPContext())
                {
                    foreach (var idx in signed.Verify(ctx))
                    {
                        if (!idx.Verify())
                            throw new SecurityException("Signature of MIME message was not valid");
                        signatures.Add(new Node("fingerprint", idx.SignerCertificate.Fingerprint.ToLower()));
                    }
                }
                tmp.Add(signatures);

                // Then traversing content of multipart/signed message.
                foreach (var idx in signed)
                {
                    Parse(tmp, idx);
                }
            }
            else if (entity is Multipart multi)
            {
                // Multipart content.
                foreach (var idx in multi)
                {
                    Parse(tmp, idx);
                }
            }
            else if (entity is TextPart text)
            {
                // Singular content type.
                // Notice! We don't really care about the encoding the text was encoded with.
                tmp.Add(new Node("content", text.GetText(out var encoding)));
            }
            node.Add(tmp);
        }

        /// <summary>
        /// Helper method to dispose a MimeEntity's streams.
        /// </summary>
        /// <param name="entity">Entity to iterate over to dispose all associated streams.</param>
        public static void Dispose(MimeEntity entity)
        {
            if (entity is MimePart part)
            {
                part.Content?.Stream?.Dispose();
            }
            else if (entity is Multipart multi)
            {
                foreach (var idx in multi)
                {
                    Dispose(idx);
                }
            }
        }

        #region [ -- Private helper methods -- ]

        /*
         * Process MIME entity's headers, and adds up into node collection.
         */
        static void ProcessHeaders(Node node, MimeEntity entity)
        {
            var headers = new Node("headers");
            foreach (var idx in entity.Headers)
            {
                if (idx.Id == HeaderId.ContentType)
                    continue; // Ignored, since it's part of main "entity" node.

                headers.Add(new Node(idx.Field, idx.Value));
            }

            // We only add headers node if there are any headers.
            if (headers.Children.Any())
                node.Add(headers);
        }

        #endregion
    }
}
