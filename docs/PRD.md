# Product Requirements Document (PRD) — CompressMyWeb

## Visão Geral do Produto
O **CompressMyWeb** é um aplicativo desktop nativo e multiplataforma (Linux, Windows e macOS), desenvolvido em **Avalonia UI (.NET 8)** com arquitetura **MVVM**, projetado para comprimir, redimensionar e converter imagens com alta performance e processamento em lote local.

---

## 1. Por quê? (Contexto e Motivação)

### O Problema
* **Impacto no Desempenho Web e SEO:** Imagens pesadas em formatos legados (JPEG, PNG, BMP) representam a maior parte dos bytes transferidos em páginas web, prejudicando o tempo de carregamento (LCP - *Largest Contentful Paint*), a pontuação no Google PageSpeed e o ranqueamento nos mecanismos de busca.
* **Limitações das Ferramentas Online:**
  * Dependência de conexão com a internet e upload lento de arquivos pesados.
  * Limites de tamanho, quantidade de imagens diárias e paywalls frequentes.
  * Risco de privacidade e conformidade (dados/imagens de clientes enviados para servidores de terceiros).
* **Complexidade de Ferramentas CLI / Avançadas:** Linhas de comando como `cwebp` ou editores pesados (Photoshop/GIMP) não são práticos para conversões rápidas no fluxo de trabalho de desenvolvedores e criadores de conteúdo.

### A Oportunidade
O formato **WebP** oferece compressão com e sem perdas superior (reduções de 25% a mais de 70% em relação ao JPEG e PNG tradicionais), mantendo transparência e fidelidade visual. Uma ferramenta desktop rápida, intuitiva e focada em WebP preenche essa lacuna de produtividade.

---

## 2. Para quê? (Objetivos e Proposta de Valor)

### Proposta de Valor
Oferecer uma solução desktop **100% local, rápida, segura e sem limites** para transformar qualquer coleção de imagens em arquivos WebP ultraleves e prontos para publicação na web.

### Objetivos do Produto
1. **Reduzir o tamanho dos arquivos:** Permitir ganhos massivos de compressão com perda mínima perceptível de qualidade.
2. **Aumentar a produtividade:** Permitir que o usuário converta dezenas ou centenas de imagens via *drag-and-drop* com um único clique.
3. **Privacidade e Autonomia:** Processamento 100% *offline*, sem envio de dados para servidores externos.
4. **Controle e Flexibilidade:** Permitir tanto modos rápidos baseados em *presets* (ex: "E-commerce", "Blog", "Hero Banner", "Fidelidade Máxima") quanto controle manual detalhado de qualidade, dimensões e metadados.

### Público-Alvo
* **Desenvolvedores Web & Front-end:** Otimização de assets estáticos de sites e aplicações.
* **Criadores de Conteúdo & Bloggers:** Redução de imagens para WordPress, Shopify e landing pages.
* **Designers UI/UX:** Exportação rápida de mockups e componentes gráficos otimizados.
* **E-commerces & Lojas Virtuais:** Padronização e compressão de catálogos de fotos de produtos.

---

## 3. Como? (Arquitetura e Implementação Técnica)

### Arquitetura da Aplicação
A aplicação segue o padrão **MVVM (Model-View-ViewModel)** garantindo desacoplamento entre interface, estado e motor de processamento:

```
┌─────────────────────────────────────────────────────────┐
│                       Avalonia UI                       │
│  (Views XAML: Drag & Drop, Sliders, Preview, Progresso) │
└────────────────────────────┬────────────────────────────┘
                             │ Data Binding & Commands
┌────────────────────────────▼────────────────────────────┐
│                  ViewModels (CommunityToolkit)          │
│   (Gerenciamento de Estado, Validações, Paralelismo)    │
└────────────────────────────┬────────────────────────────┘
                             │ Serviços
┌────────────────────────────▼────────────────────────────┐
│                    Services / Engine                    │
│    (SixLabors.ImageSharp: Decoders, WebP Encoder, Resizer)│
└─────────────────────────────────────────────────────────┘
```

### Motor de Processamento
* **Biblioteca Core:** `SixLabors.ImageSharp` e `SixLabors.ImageSharp.Drawing`.
* **Fluxo de Conversão:**
  1. Leitura e decodificação do formato de origem (PNG, JPEG, BMP, GIF, TIFF).
  2. Ajuste de escala/redimensionamento proporcional opcional (Max Width / Max Height).
  3. Codificação WebP (`WebpEncoder`):
     * Modo Lossy (Qualidade de 1 a 100).
     * Modo Lossless (Sem perdas, ideal para ícones, prints e ilustrações).
     * Opção de preservação ou remoção de metadados (EXIF) para ganho extra de espaço.
  4. Salvamento assíncrono mantendo ou personalizando a nomenclatura de saída.

### Fluxo de Trabalho do Usuário (User Journey)
1. **Entrada:** Usuário arrasta arquivos ou pastas para a janela da aplicação (ou usa o seletor de arquivos).
2. **Configuração:**
   * Ajusta o nível de qualidade desejado (ex: 80%).
   * Define limites de redimensionamento (ex: largura máxima de 1920px).
   * Escolhe o diretório de destino (mesma pasta de origem ou pasta customizada).
3. **Visualização:** Prévia com estimativa de redução de tamanho antes/depois.
4. **Execução:** Clique em "Comprimir / Converter". Processamento concorrente em background usando `Parallel.ForEachAsync` / `Task` com barra de progresso em tempo real e cancelamento via `CancellationToken`.
5. **Resultado:** Resumo com total de megabytes economizados e tempo decorrido.

---

## 4. Requisitos Funcionais

| ID | Requisito | Descrição |
|---|---|---|
| **RF-01** | Drag & Drop | Suporte a arrastar e soltar múltiplos arquivos e pastas diretamente na UI. |
| **RF-02** | Suporte a Formatos de Entrada | JPEG, JPG, PNG, BMP, TIFF, GIF e WebP existente para recompresão. |
| **RF-03** | Formatos de saída | Geração de WebP, JPEG, PNG, PDF ou manutenção do formato original. Imagens podem gerar um PDF individual; WebP, JPEG e o JPEG interno do PDF usam o controle de qualidade. |
| **RF-04** | Redimensionamento Inteligente | Opção de redimensionar proporcionalmente por largura/altura máxima ou percentual. |
| **RF-05** | Limpeza de Metadados | Opção para remover dados EXIF/ICC para menor tamanho final. |
| **RF-06** | Presets Prontos | Presets de configuração rápida (ex: Web Ideal, Alta Qualidade, Máxima Compressão). |
| **RF-07** | Fila & Progresso em Tempo Real | Lista de arquivos com status individual (Pendente, Processando, Concluído, Erro) e barra geral. |
| **RF-08** | Estatísticas de Economia | Exibição de tamanho original vs tamanho final e porcentagem de redução. |
| **RF-09** | Cancelamento | Possibilidade de pausar ou cancelar a fila em execução a qualquer momento. |
| **RF-10** | Compressão de PDF | Compressão estrutural de PDFs existentes com qpdf, preservando textos, links e vetores e oferecendo otimização opcional das imagens internas. |

> A regravação de um PDF invalida assinaturas digitais existentes. PDFs protegidos por senha não são processados enquanto não houver suporte explícito e seguro para credenciais.

---

## 5. Requisitos Não-Funcionais

* **Performance:** Uso eficiente de CPU multi-core para processamento paralelo de imagens.
* **Baixa Pegada de Memória:** Descarte imediato de buffers e streams após processamento de cada imagem.
* **Interface Responsiva:** Thread de UI nunca bloqueada durante o processamento pesado.
* **Multiplataforma:** Suporte nativo para Linux (X11/Wayland), Windows (10/11) e macOS.
* **Zero Telemetria / Privacidade:** Nenhum dado sai da máquina do usuário.

---

## 6. Stack Tecnológica

* **Plataforma:** .NET 8.0 SDK
* **Interface Gráfica:** Avalonia UI (11.2+)
* **Padrão de Arquitetura:** MVVM com `CommunityToolkit.Mvvm` (Source Generators)
* **Processamento de Imagens:** `SixLabors.ImageSharp` (4.1+)
