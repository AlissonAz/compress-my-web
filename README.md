# Compress my Web

Aplicativo desktop gratuito para comprimir e converter imagens e arquivos PDF localmente, sem envio de arquivos para servidores externos.

## Recursos

- Conversão de imagens para WebP, JPEG, PNG e PDF.
- Compressão estrutural e agressiva de PDFs.
- Processamento em lote de arquivos e pastas.
- Presets de qualidade, redimensionamento e remoção de metadados.
- Processamento local, sem telemetria.

## Requisitos

- Linux x64 ou Windows x64.
- No Windows, qpdf e Ghostscript já estão incluídos no instalador offline.
- No Linux, instale os pacotes `qpdf` e `ghostscript` da distribuição.
- O SDK .NET 8 é necessário somente para compilar o código-fonte.

## Compilar

```bash
dotnet restore
dotnet build
dotnet run
```

Pacote Debian:

```bash
bash build-deb.sh
```

Pacote Debian offline (qpdf e Ghostscript incluídos):

```bash
dotnet restore -r linux-x64
bash build-deb-offline.sh
```

O pacote offline é destinado a Linux amd64 e ainda requer apenas as bibliotecas básicas do sistema (`libc6`, `libgcc-s1` e `libstdc++6`).

Instalador Windows offline, com `curl`, `7z` e NSIS disponíveis no ambiente de compilação:

```bash
bash build-windows.sh
```

O script baixa versões fixadas do qpdf e Ghostscript, confere seus SHA-256 oficiais e inclui as dependências no instalador. Depois do primeiro build, os downloads ficam no cache local.

## Privacidade

O processamento é realizado integralmente no computador do usuário. O aplicativo não possui telemetria e não envia arquivos ou estatísticas pela internet.

## Licença

Compress my Web é software livre distribuído sob a GNU Affero General Public License, versão 3 ou posterior (`AGPL-3.0-or-later`). Consulte [LICENSE](LICENSE).

Copyright © 2026 Alisson Azevedo.

## Dependências

As dependências mantêm suas próprias licenças. Consulte [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Apoie o projeto

O aplicativo continuará gratuito. Doações são opcionais e não concedem recursos exclusivos. Os canais oficiais de apoio serão informados no repositório público do projeto.

## Segurança

Consulte [SECURITY.md](SECURITY.md) antes de comunicar uma vulnerabilidade.
