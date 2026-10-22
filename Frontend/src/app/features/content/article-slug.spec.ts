import { articleSlug } from './article-slug';
describe('Localized article slugs', () => {
  for (const [title, expected] of [
    ['English article', 'english-article'], ['Русская статья', 'russkaya-statya'],
    ['Český článek', 'cesky-clanek'], ['Größter Überblick', 'grosster-uberblick'],
    ['Reseña española', 'resena-espanola']
  ]) it('creates an ASCII slug for ' + title, () => expect(articleSlug(title)).toBe(expected));
});
