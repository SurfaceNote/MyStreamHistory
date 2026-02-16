export interface SocialLink {
  socialNetworkType: string;
  path: string;
  fullUrl: string;
}

export enum SocialNetworkType {
  Twitch = 'Twitch',
  YouTube = 'YouTube',
  Instagram = 'Instagram',
  Discord = 'Discord',
  Steam = 'Steam',
  VK = 'VK',
  Yandex = 'Yandex',
  Telegram = 'Telegram'
}

export interface GetSocialLinksResponse {
  socialLinks: SocialLink[];
}

export interface UpdateSocialLinksRequest {
  socialLinks: SocialLink[];
}

export interface UpdateSocialLinksResponse {
  success: boolean;
  error?: string;
}

