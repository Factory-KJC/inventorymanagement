/** Parses complete Server-Sent Events from an HTTP response body. */
export async function* parseServerSentEvents(body) {
  const decoder = new TextDecoder();
  let buffer = '';

  for await (const chunk of body) {
    buffer += decoder.decode(chunk, { stream: true });
    buffer = buffer.replaceAll('\r\n', '\n');
    let boundary;
    while ((boundary = buffer.indexOf('\n\n')) >= 0) {
      yield buffer.slice(0, boundary);
      buffer = buffer.slice(boundary + 2);
    }
  }
}
