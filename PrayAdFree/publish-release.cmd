@echo off
setlocal

if "%PRAYADFREE_KEYSTORE%"=="" (
  echo PRAYADFREE_KEYSTORE is not set.
  exit /b 1
)

if "%PRAYADFREE_KEY_ALIAS%"=="" (
  echo PRAYADFREE_KEY_ALIAS is not set.
  exit /b 1
)

if "%PRAYADFREE_STORE_PASS%"=="" (
  echo PRAYADFREE_STORE_PASS is not set.
  exit /b 1
)

if "%PRAYADFREE_KEY_PASS%"=="" (
  echo PRAYADFREE_KEY_PASS is not set.
  exit /b 1
)

dotnet publish "%~dp0PrayAdFree.csproj" ^
  -c Release ^
  -f net10.0-android ^
  -p:RuntimeIdentifier=android-arm64 ^
  -p:AndroidPackageFormat=aab ^
  -p:AndroidKeyStore=true ^
  -p:AndroidSigningKeyStore="%PRAYADFREE_KEYSTORE%" ^
  -p:AndroidSigningKeyAlias="%PRAYADFREE_KEY_ALIAS%" ^
  -p:AndroidSigningStorePass="%PRAYADFREE_STORE_PASS%" ^
  -p:AndroidSigningKeyPass="%PRAYADFREE_KEY_PASS%"
