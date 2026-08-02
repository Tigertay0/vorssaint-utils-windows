// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Vorssaint

import AppKit
import CoreGraphics

/// Captures a selected region repeatedly while scrolling the focused app, then
/// stitches the slices into one tall screenshot. This stays app-agnostic:
/// browsers, documents and chat panes all receive the same real scroll event.
enum ScreenshotScrollingCapture {
    static func capture(region: RecorderSupport.Region,
                        includePointer: Bool) async -> ScreenshotSelectionController.Capture? {
        try? await Task.sleep(nanoseconds: 140_000_000)

        let overlap = ScreenshotSupport.scrollingCaptureOverlapPixels(
            regionHeight: region.anchorRect.height,
            scale: region.scale)
        let step = ScreenshotSupport.scrollingCaptureStepPoints(
            regionHeight: region.anchorRect.height)

        var slices: [CGImage] = []
        for index in 0..<ScreenshotSupport.scrollingCaptureMaxFrames {
            guard let image = await ScreenshotCaptureEngine.captureDisplayRegion(
                displayID: region.displayID,
                pixelRect: region.pixelRect,
                includePointer: includePointer)
            else { break }

            if let previous = slices.last, areVisuallySame(previous, image) {
                break
            }
            slices.append(image)

            if index < ScreenshotSupport.scrollingCaptureMaxFrames - 1 {
                postScrollDown(points: step)
                try? await Task.sleep(nanoseconds: 260_000_000)
            }
        }

        guard let image = stitch(slices, fallbackOverlapPixels: overlap) else { return nil }
        return ScreenshotSelectionController.Capture(image: image,
                                                     scale: region.scale,
                                                     anchorRect: region.anchorRect)
    }

    private static func postScrollDown(points: CGFloat) {
        let lines = max(1, Int32((points / 18).rounded()))
        guard let event = CGEvent(scrollWheelEvent2Source: nil,
                                  units: .line,
                                  wheelCount: 1,
                                  wheel1: -lines,
                                  wheel2: 0,
                                  wheel3: 0)
        else { return }
        event.post(tap: .cghidEventTap)
    }

    private struct StitchSlice {
        let image: CGImage
        let topCrop: Int
    }

    private struct SampledImage {
        let width: Int
        let height: Int
        let bytes: [UInt8]
    }

    private static func stitch(_ slices: [CGImage],
                               fallbackOverlapPixels: Int) -> CGImage? {
        guard let first = slices.first else { return nil }
        guard slices.count > 1 else { return first }

        let width = slices.map(\.width).min() ?? first.width
        var stitchSlices = [StitchSlice(image: first, topCrop: 0)]
        for index in 1..<slices.count {
            let image = slices[index]
            let measuredOverlap = overlapPixels(previous: slices[index - 1],
                                                current: image,
                                                fallback: fallbackOverlapPixels)
            stitchSlices.append(StitchSlice(image: image,
                                            topCrop: min(measuredOverlap, image.height - 1)))
        }

        let pieceHeights = stitchSlices.map { slice in
            max(1, slice.image.height - slice.topCrop)
        }
        let height = pieceHeights.reduce(0, +)

        guard let context = CGContext(data: nil,
                                      width: width,
                                      height: height,
                                      bitsPerComponent: 8,
                                      bytesPerRow: 0,
                                      space: CGColorSpaceCreateDeviceRGB(),
                                      bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)
        else { return nil }
        context.interpolationQuality = .none

        var y = height
        for slice in stitchSlices {
            guard let piece = slice.image.cropping(to: CGRect(x: 0,
                                                              y: slice.topCrop,
                                                              width: width,
                                                              height: slice.image.height - slice.topCrop))
            else { continue }
            y -= piece.height
            context.draw(piece, in: CGRect(x: 0,
                                           y: y,
                                           width: piece.width,
                                           height: piece.height))
        }
        return context.makeImage()
    }

    private static func overlapPixels(previous: CGImage,
                                      current: CGImage,
                                      fallback: Int) -> Int {
        guard let previousSample = verticalSampleBytes(previous),
              let currentSample = verticalSampleBytes(current),
              previousSample.width == currentSample.width
        else { return fallback }

        let maxOverlap = max(1, min(previousSample.height, currentSample.height) - 1)
        let fallbackOverlap = min(max(1, fallback), maxOverlap)
        let coarseStep = max(1, maxOverlap / 160)
        var candidates = Set<Int>([fallbackOverlap, maxOverlap])
        for overlap in stride(from: 1, through: maxOverlap, by: coarseStep) {
            candidates.insert(overlap)
        }

        var bestOverlap = fallbackOverlap
        var bestRawScore = Double.greatestFiniteMagnitude
        var bestScore = Double.greatestFiniteMagnitude
        for overlap in candidates {
            guard let rawScore = overlapScore(previous: previousSample,
                                             current: currentSample,
                                             overlap: overlap)
            else { continue }
            let prior = Double(abs(overlap - fallbackOverlap)) * 0.01
            let score = rawScore + prior
            if score < bestScore {
                bestOverlap = overlap
                bestRawScore = rawScore
                bestScore = score
            }
        }

        if coarseStep > 1 {
            let lowerBound = max(1, bestOverlap - coarseStep)
            let upperBound = min(maxOverlap, bestOverlap + coarseStep)
            for overlap in lowerBound...upperBound {
                guard let rawScore = overlapScore(previous: previousSample,
                                                 current: currentSample,
                                                 overlap: overlap)
                else { continue }
                let prior = Double(abs(overlap - fallbackOverlap)) * 0.01
                let score = rawScore + prior
                if score < bestScore {
                    bestOverlap = overlap
                    bestRawScore = rawScore
                    bestScore = score
                }
            }
        }

        return bestRawScore <= 28 ? bestOverlap : fallbackOverlap
    }

    private static func overlapScore(previous: SampledImage,
                                     current: SampledImage,
                                     overlap: Int) -> Double? {
        let bandHeight = min(96, overlap)
        guard bandHeight > 0, overlap < previous.height, overlap < current.height else {
            return nil
        }

        var starts = [0]
        let middleStart = max(0, overlap / 2 - bandHeight / 2)
        let bottomStart = max(0, overlap - bandHeight)
        if !starts.contains(middleStart) { starts.append(middleStart) }
        if !starts.contains(bottomStart) { starts.append(bottomStart) }

        var totalDifference = 0
        var count = 0
        for start in starts {
            let previousStart = previous.height - overlap + start
            let currentStart = start
            guard previousStart >= 0,
                  previousStart + bandHeight <= previous.height,
                  currentStart + bandHeight <= current.height
            else { continue }

            for rowOffset in 0..<bandHeight {
                let previousRow = (previousStart + rowOffset) * previous.width * 4
                let currentRow = (currentStart + rowOffset) * current.width * 4
                for column in 0..<previous.width {
                    let previousIndex = previousRow + column * 4
                    let currentIndex = currentRow + column * 4
                    for channel in 0..<3 {
                        totalDifference += abs(Int(previous.bytes[previousIndex + channel])
                            - Int(current.bytes[currentIndex + channel]))
                        count += 1
                    }
                }
            }
        }

        guard count > 0 else { return nil }
        return Double(totalDifference) / Double(count)
    }

    private static func verticalSampleBytes(_ image: CGImage) -> SampledImage? {
        let width = 32
        let height = image.height
        var data = [UInt8](repeating: 0, count: width * height * 4)
        guard let context = CGContext(data: &data,
                                      width: width,
                                      height: height,
                                      bitsPerComponent: 8,
                                      bytesPerRow: width * 4,
                                      space: CGColorSpaceCreateDeviceRGB(),
                                      bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)
        else { return nil }
        context.interpolationQuality = .low
        context.draw(image, in: CGRect(x: 0, y: 0, width: width, height: height))
        return SampledImage(width: width, height: height, bytes: data)
    }

    private static func areVisuallySame(_ lhs: CGImage, _ rhs: CGImage) -> Bool {
        guard let lhsData = thumbnailBytes(lhs),
              let rhsData = thumbnailBytes(rhs),
              lhsData.count == rhsData.count
        else { return false }
        var totalDifference = 0
        for index in lhsData.indices {
            totalDifference += abs(Int(lhsData[index]) - Int(rhsData[index]))
        }
        let average = Double(totalDifference) / Double(lhsData.count)
        return average < 1.5
    }

    private static func thumbnailBytes(_ image: CGImage) -> [UInt8]? {
        let width = 24
        let height = 24
        var data = [UInt8](repeating: 0, count: width * height * 4)
        guard let context = CGContext(data: &data,
                                      width: width,
                                      height: height,
                                      bitsPerComponent: 8,
                                      bytesPerRow: width * 4,
                                      space: CGColorSpaceCreateDeviceRGB(),
                                      bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)
        else { return nil }
        context.interpolationQuality = .low
        context.draw(image, in: CGRect(x: 0, y: 0, width: width, height: height))
        return data
    }
}
