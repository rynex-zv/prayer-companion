dotnet publish "PrayAdFree.csproj" -c Release -f net10.0-android ^
  -p:AndroidPackageFormat=aab ^
  -p:AndroidKeyStore=true ^
  -p:AndroidSigningKeyStore="F:\coding\local\PrayAdFree\PrayAdFree\PrayAdFree\keystore.jks" ^
  -p:AndroidSigningKeyAlias=release ^
  -p:AndroidSigningStorePass=C9v#2tL!RZ7mX4pR@hF2 ^
  -p:AndroidSigningKeyPass=C9v#2tL!RZ7mX4pR@hF2 && pause
v1Dke9RI3oZ6vA2
ZYPNi,o3T/!r%\9O=E*u:GHz5A#)7D

dotnet build "PrayAdFree/PrayAdFree.csproj" -t:Install -c Release -f net10.0-android -p:AndroidDebuggable=true