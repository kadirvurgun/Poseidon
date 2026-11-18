import ar from './common/ar.json' with { type: 'json' }
import cs from './common/cs.json' with { type: 'json' }
import hu from './common/hu.json' with { type: 'json' }
import fr from './common/fr.json' with { type: 'json' }
import en from './common/en.json' with { type: 'json' }
import fi from './common/fi.json' with { type: 'json' }
import enGB from './common/en-GB.json' with { type: 'json' }
import zhHans from './common/zh-Hans.json' with { type: 'json' }
import hi from './common/hi.json' with { type: 'json' }
import sk from './common/sk.json' with { type: 'json' }
import tr from './common/tr.json' with { type: 'json' }
import ru from './common/ru.json' with { type: 'json' }
import ptBR from './common/pt-BR.json' with { type: 'json' }
import it from './common/it.json' with { type: 'json' }
import es from './common/es.json' with { type: 'json' }
import de from './common/de.json' with { type: 'json' }
import zhHant from './common/zh-Hant.json' with { type: 'json' }
import type { Locale } from './locales'

export type CommonMessageKey = keyof typeof en
// These are authored UI translations, not regulatory or expert approvals.
export const commonMessages: Record<Locale, Record<CommonMessageKey, string>> = {
  ar, cs, hu, fr, en, fi, 'en-GB': enGB, 'zh-Hans': zhHans, hi, sk, tr, ru, 'pt-BR': ptBR, it, es, de, 'zh-Hant': zhHant,
}
