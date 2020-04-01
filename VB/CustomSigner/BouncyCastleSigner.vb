﻿Imports DevExpress.Pdf
Imports Org.BouncyCastle.Asn1
Imports Org.BouncyCastle.Asn1.X509
Imports Org.BouncyCastle.Crypto
Imports Org.BouncyCastle.Crypto.Digests
Imports Org.BouncyCastle.Crypto.Encodings
Imports Org.BouncyCastle.Crypto.Engines
Imports Org.BouncyCastle.Pkcs
Imports Org.BouncyCastle.Security
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq

Namespace CustomSigner
	' Declare a custom class to calculate a digest value:
	Public Class BouncyCastleDigestCalculator
		Implements IDigestCalculator

		Private ReadOnly digest As IDigest

		Public AlgorithmOid As Function(String) DigestUtilities.GetObjectIdentifier(digest.AlgorithmName).Id

		Public Sub New()
			digest = New Sha512Digest()
		End Sub
		Public Function ComputeDigest(ByVal stream As Stream) As Byte()
			digest.Reset()
			Dim buffer((1024 * 1024) - 1) As Byte
			Dim readByteCount As Integer
			Do
				readByteCount = stream.Read(buffer, 0, buffer.Length)
				If readByteCount <> 0 Then
					digest.BlockUpdate(buffer, 0, readByteCount)
				End If
			Loop While readByteCount <> 0
			Dim result(GetDigestSize() - 1) As Byte
			digest.DoFinal(result, 0)
			Return result
		End Function
		Public Function GetDigestSize() As Integer
			Return digest.GetDigestSize()
		End Function
	End Class

	Public Class BouncyCastleSigner
		Inherits Pkcs7SignerBase

		'Specify the signing algoritm's OID:
		Private Const PKCS1RsaEncryption As String = "1.2.840.113549.1.1.1"

		Private ReadOnly certificate()() As Byte
		Private ReadOnly rsaEngine As IAsymmetricBlockCipher

		'Specify a custom digest calculator:
		Protected Overrides ReadOnly Property DigestCalculator() As IDigestCalculator
			Get
				Return New BouncyCastleDigestCalculator()
			End Get
		End Property
		Protected String As override

		Public Sub New(ByVal file As String, ByVal password As String, ByVal tsaClient As ITsaClient)
			MyBase.New(tsaClient)
			'Read PKCS#12 file:
			Dim pkcs = New Pkcs12Store(System.IO.File.Open(file, FileMode.Open), password.ToCharArray())

			'Get the certificate's alias:
			Dim [alias] = pkcs.Aliases.OfType(Of String)().First()

			'Get the certificate's chain:
			certificate = pkcs.GetCertificateChain([alias]).Select(Function(c) c.Certificate.GetEncoded()).ToArray()

			'Initialize the encryption engine:
			rsaEngine = New Pkcs1Encoding(New RsaBlindedEngine())
			rsaEngine.Init(True, pkcs.GetKey([alias]).Key)
		End Sub

		Protected Overrides Function GetCertificates() As IEnumerable(Of Byte())
			Return certificate
		End Function

		Protected Overrides Function SignDigest(ByVal digest() As Byte) As Byte()
			'Create the digest info object
			'Encrypted by the signer's private key:
			Dim dInfo = New DigestInfo(New AlgorithmIdentifier(New DerObjectIdentifier(DigestCalculator.AlgorithmOid), DerNull.Instance), digest)
			Dim digestInfo() As Byte = dInfo.GetDerEncoded()
			Return rsaEngine.ProcessBlock(digestInfo, 0, digestInfo.Length)
		End Function
	End Class

End Namespace