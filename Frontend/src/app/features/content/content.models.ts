export type ArticleType = 'news' | 'review';

export interface ContentNode {
  type: string;
  text?: string;
  attrs?: Record<string, any>;
  marks?: { type: string; attrs?: Record<string, any> }[];
  content?: ContentNode[];
}

export interface ArticleInput {
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
  id: string;
  slugLocked: boolean;
  publishedAt: string | null;
  updatedAt: string;
}

export interface ArticleSummary {
  id: string;
  type: ArticleType;
  slug: string;
  title: string;
  summary: string;
  coverId: string | null;
  publishedAt: string | null;
  updatedAt?: string;
  revision?: number;
}

export interface PublicArticle extends Omit<ArticleInput, 'revision'> {
  id: string;
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
