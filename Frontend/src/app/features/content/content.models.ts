import { Language } from '../../i18n/languages';
export interface ArticleTranslation { id: string; language: Language; type: ArticleType; slug: string; path: string; publishedAt: string | null; }
export type ArticleType = 'news' | 'review';

export interface ContentNode {
  type: string;
  text?: string;
  attrs?: Record<string, any>;
  marks?: { type: string; attrs?: Record<string, any> }[];
  content?: ContentNode[];
}

export interface ArticleInput {
  language?: Language;
  type: ArticleType;
  slug: string;
  title: string;
  summary: string;
  seoTitle: string;
  seoDescription: string;
  body: ContentNode;
  coverId: string | null;
  revision: number;
}

export interface Article extends ArticleInput {
  language: Language;
  id: string;
  translationGroupId: string;
  translations: ArticleTranslation[];
  slugLocked: boolean;
  publishedAt: string | null;
  updatedAt: string;
}

export interface ArticleSummary {
  language?: Language;
  id: string;
  type: ArticleType;
  slug: string;
  title: string;
  summary: string;
  coverId: string | null;
  coverUrl?: string | null;
  publishedAt: string | null;
  updatedAt?: string;
  revision?: number;
}

export interface PublicArticle extends Omit<ArticleInput, 'revision'> {
  language: Language;
  translationGroupId: string;
  translations: ArticleTranslation[];
  id: string;
  coverUrl?: string | null;
  publishedAt: string;
}

export interface PageResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface MediaAsset {
  id: string;
  fileName: string;
  contentType: string;
  size: number;
  createdAt: string;
}
