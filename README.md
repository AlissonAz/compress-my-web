# Compress my Web — compressor de imagens e PDF, conversor, unir e dividir PDF

O **Compress my Web** é um aplicativo desktop gratuito e de código aberto para **comprimir imagens**, **converter imagens**, **comprimir PDF**, **unir PDFs** e **dividir PDF** no Windows e Linux.

Todos os arquivos são processados localmente, no computador do usuário. O aplicativo não envia documentos, imagens, estatísticas ou telemetria para servidores externos.

**Palavras-chave:** compressor de imagens, compressor de PDF, reduzir tamanho de PDF, converter JPG para WebP, converter PNG para WebP, converter imagem para PDF, unir PDF, juntar PDF, mesclar PDF, dividir PDF, separar páginas de PDF, editor de PDF offline, ferramenta de imagem para web.

## O que o programa faz

- Comprime imagens JPG, JPEG, PNG, BMP, TIFF, GIF e WebP.
- Converte imagens para WebP, JPEG, PNG ou PDF.
- Comprime PDFs mantendo textos, links e vetores sempre que possível.
- Une vários arquivos PDF em um único documento, na ordem escolhida.
- Divide um PDF em uma página por arquivo ou em intervalos, como `1-3,5,8-10`.
- Processa arquivos e pastas em lote.
- Remove metadados, quando solicitado, e oferece presets de qualidade.
- Mantém a resolução original por padrão; redimensionamento é opcional.

## Instalação

### Windows 10 e Windows 11

1. Baixe `CompressMyWeb-Setup-1.6.0-win-x64.exe` na página de [Releases](https://github.com/AlissonAz/compress-my-web/releases).
2. Execute o instalador e siga as etapas na tela.
3. Abra **Compress my Web** pelo Menu Iniciar.

O instalador Windows é offline: ele já inclui `qpdf` e Ghostscript para trabalhar com PDFs.

### Linux Mint, Ubuntu e Debian (amd64/x64)

1. Baixe `compressmyweb_1.6.0_amd64.deb` na página de [Releases](https://github.com/AlissonAz/compress-my-web/releases).
2. Abra o terminal na pasta onde o arquivo foi baixado.
3. Instale o pacote:

```bash
sudo apt install ./compressmyweb_1.6.0_amd64.deb
```

O pacote instala as dependências necessárias, incluindo `qpdf` e Ghostscript. Depois, abra **CompressMyWeb** pelo menu de aplicativos.

## Como usar

Ao abrir o programa, escolha uma ferramenta na tela inicial: **Comprimir**, **Converter**, **Unir PDF** ou **Dividir PDF**.

### Comprimir imagens e PDF

1. Clique em **Comprimir**.
2. Use **Adicionar Arquivos** ou arraste imagens e PDFs para a fila.
3. Escolha um preset ou ajuste qualidade, metadados e pasta de destino.
4. Clique em **Iniciar compressão**.

O preset Web mantém a resolução original. Para reduzir largura e altura, abra **Opções** e marque **Limitar resolução**.

### Converter imagens

1. Clique em **Converter**.
2. Adicione as imagens.
3. Escolha o formato de saída: WebP, JPEG, PNG ou PDF.
4. Clique em **Iniciar conversão**.

O modo Converter começa com o preset **Sem perdas / Não comprimir**, sem redimensionamento. Ajuste as opções somente se desejar reduzir tamanho ou resolução.

### Unir ou juntar PDFs

1. Clique em **Unir PDF**.
2. Clique em **Adicionar PDFs** e selecione dois ou mais arquivos.
3. Use as setas ↑ e ↓ para definir a ordem das páginas.
4. Escolha a pasta e informe o nome do arquivo final.
5. Clique em **Unir PDFs**.

Se já existir um arquivo com o mesmo nome, o programa preserva o original e cria automaticamente um novo nome, como `pdf-unido-1.pdf`.

### Dividir ou separar páginas de PDF

1. Clique em **Dividir PDF** e selecione o PDF de origem.
2. Escolha **Cada página em um PDF** ou **Intervalos personalizados**.
3. Para intervalos, escreva por exemplo `1-3,5,8-10`.
4. Escolha pasta, prefixo dos arquivos e clique em **Dividir PDF**.

No modo por página, o resultado será semelhante a `documento-pagina-1.pdf`. No modo por intervalos, `1-3,5` gera `documento-parte-1-3.pdf` e `documento-parte-5.pdf`.

## Arquivos de saída e privacidade

Por padrão, os resultados são salvos em:

```text
Imagens/Arquivos Compress-my-web
```

Você pode trocar essa pasta em cada ferramenta. Os arquivos originais são preservados por padrão.

O processamento é local e offline. Isso é útil para documentos sensíveis, fotos de clientes e PDFs que não devem ser enviados a ferramentas online.

## Limitações conhecidas

- PDFs protegidos por senha não são processados sem suporte explícito a credenciais.
- Regravar um PDF pode invalidar assinaturas digitais existentes.
- Para arquivos muito grandes, mantenha espaço livre em disco para os arquivos temporários e de saída.

## Compilar a partir do código-fonte

Pré-requisito: SDK .NET 8.

```bash
dotnet restore
dotnet build
dotnet run
```

### Gerar pacote Debian

```bash
bash build-deb.sh
```

### Gerar instalador Windows offline

Pré-requisitos: `curl`, `7z` e NSIS (`nsis` e `nsis-common`).

```bash
bash build-windows.sh
```

O script baixa versões fixadas de `qpdf` e Ghostscript, confere seus hashes SHA-256 e inclui essas dependências no instalador Windows.

## Segurança, licença e dependências

Consulte [SECURITY.md](SECURITY.md) antes de comunicar uma vulnerabilidade.

O Compress my Web é software livre distribuído sob a [GNU AGPL-3.0-or-later](LICENSE). As dependências mantêm suas próprias licenças; veja [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

Copyright © 2026 Alisson Azevedo.
